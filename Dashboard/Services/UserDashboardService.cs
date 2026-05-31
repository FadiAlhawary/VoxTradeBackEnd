using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Dashboard.DTOs;
using VoxTrade.Dashboard.Interfaces;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Dashboard.Services;

public class UserDashboardService : IUserDashboardService
{
    private readonly TradingDbContext _context;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ILogger<UserDashboardService> _logger;

    public UserDashboardService(
        TradingDbContext context,
        IPortfolioRepository portfolioRepository,
        ILogger<UserDashboardService> logger)
    {
        _context = context;
        _portfolioRepository = portfolioRepository;
        _logger = logger;
    }

    private async Task<IDbConnection> GetOpenConnection()
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
        return connection;
    }

    public async Task<UserDashboardSummaryDto?> GetSummaryAsync(int userId, int recentActivityLimit = 10)
    {
        try
        {
            var connection = await GetOpenConnection();

            var header = await connection.QueryFirstOrDefaultAsync<UserDashboardSummaryDto>(
                """
                SELECT
                    u.id AS UserId,
                    u.username AS Username,
                    u.first_name_en AS FirstNameEn,
                    u.last_name_en AS LastNameEn,
                    COALESCE(u.is_locked, false) AS IsLocked,
                    COALESCE(u.is_deleted, false) AS IsDeleted
                FROM public.users u
                WHERE u.id = @UserId;
                """,
                new { UserId = userId });

            if (header == null)
                return null;

            header.Wallet = await GetWalletSummaryAsync(userId);
            header.Orders = await GetOrdersSummaryAsync(userId);

            var positionsSummary = await GetPositionsSummaryAsync(userId);
            header.Positions = positionsSummary;

            var cash = header.Wallet?.AvailableBalance ?? 0;
            header.TotalPortfolioValue = cash + positionsSummary.TotalMarketValue;

            var tradeCounts = await connection.QuerySingleAsync<(int Total, int Last7)>(
                """
                SELECT
                    COUNT(*)::int AS Total,
                    COUNT(*) FILTER (
                        WHERE t.executed_at >= NOW() - INTERVAL '7 days'
                    )::int AS Last7
                FROM public.trades t
                WHERE t.user_id = @UserId;
                """,
                new { UserId = userId });

            header.TotalTrades = tradeCounts.Total;
            header.TradesLast7Days = tradeCounts.Last7;
            header.RecentActivity = await GetRecentActivityAsync(userId, recentActivityLimit);

            return header;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard summary for user {UserId}", userId);
            return null;
        }
    }

    public async Task<UserDashboardWalletDto?> GetWalletSummaryAsync(int userId)
    {
        try
        {
            var connection = await GetOpenConnection();
            return await connection.QueryFirstOrDefaultAsync<UserDashboardWalletDto>(
                """
                SELECT
                    w.id AS WalletId,
                    w.user_id AS UserId,
                    w.currency_id AS CurrencyId,
                    c.symbol AS CurrencySymbol,
                    w.balance AS Balance,
                    w.available_balance AS AvailableBalance,
                    w.reserved_balance AS ReservedBalance,
                    w.status AS Status,
                    w.freeze_reason AS FreezeReason,
                    w.updated_at AS UpdatedAt
                FROM public.wallets w
                LEFT JOIN public.currencies c ON c.id = w.currency_id
                WHERE w.user_id = @UserId;
                """,
                new { UserId = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load wallet summary for user {UserId}", userId);
            return null;
        }
    }

    public async Task<UserDashboardOrdersSummaryDto> GetOrdersSummaryAsync(int userId)
    {
        try
        {
            var connection = await GetOpenConnection();
            return await connection.QuerySingleAsync<UserDashboardOrdersSummaryDto>(
                """
                SELECT
                    COUNT(*)::int AS Total,
                    COUNT(*) FILTER (WHERE st.code = 'pending')::int AS Pending,
                    COUNT(*) FILTER (WHERE st.code = 'partially_filled')::int AS PartiallyFilled,
                    COUNT(*) FILTER (WHERE st.code = 'filled')::int AS Filled,
                    COUNT(*) FILTER (WHERE st.code = 'cancelled')::int AS Cancelled,
                    COUNT(*) FILTER (
                        WHERE st.code IN ('pending', 'partially_filled')
                    )::int AS Active
                FROM public.orders o
                INNER JOIN public.lookup st ON st.id = o.status_id
                WHERE o.user_id = @UserId;
                """,
                new { UserId = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load order summary for user {UserId}", userId);
            return new UserDashboardOrdersSummaryDto();
        }
    }

    public Task<List<PortfolioPositionDto>> GetPositionsAsync(int userId) =>
        _portfolioRepository.GetPortfolio(userId);

    private async Task<UserDashboardPositionsSummaryDto> GetPositionsSummaryAsync(int userId)
    {
        try
        {
            var connection = await GetOpenConnection();
            return await connection.QuerySingleAsync<UserDashboardPositionsSummaryDto>(
                """
                SELECT
                    COUNT(*)::int AS PositionCount,
                    COALESCE(SUM(p.quantity), 0) AS TotalQuantity,
                    COALESCE(SUM(
                        p.quantity * COALESCE(mq.bid_price, mq.last_trade_price, p.average_cost)
                    ), 0) AS TotalMarketValue,
                    COALESCE(SUM(
                        (COALESCE(mq.bid_price, mq.last_trade_price, p.average_cost) - p.average_cost)
                        * p.quantity
                    ), 0) AS TotalUnrealizedPnl,
                    COALESCE(SUM(p.realized_pnl), 0) AS TotalRealizedPnl
                FROM public.positions p
                LEFT JOIN LATERAL (
                    SELECT bid_price, last_trade_price
                    FROM public.market_quotes
                    WHERE instrument_id = p.instrument_id
                      AND is_active = true
                    ORDER BY quote_timestamp DESC
                    LIMIT 1
                ) mq ON true
                WHERE p.user_id = @UserId
                  AND p.quantity > 0;
                """,
                new { UserId = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load positions summary for user {UserId}", userId);
            return new UserDashboardPositionsSummaryDto();
        }
    }

    public async Task<List<UserRecentActivityItemDto>> GetRecentActivityAsync(int userId, int limit = 20)
    {
        if (limit < 1)
            limit = 20;
        if (limit > 100)
            limit = 100;

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<UserRecentActivityItemDto>(
                """
                SELECT *
                FROM (
                    SELECT
                        'order'::text AS ActivityType,
                        o.id AS Id,
                        COALESCE(i.symbol, 'Order') AS Title,
                        CONCAT(COALESCE(ot.code, 'order'), ' · ', COALESCE(o.side, '')) AS Subtitle,
                        o.quantity AS Amount,
                        st.code AS Status,
                        o.created_at AS CreatedAt
                    FROM public.orders o
                    LEFT JOIN public.instruments i ON i.id = o.instrument_id
                    LEFT JOIN public.lookup ot ON ot.id = o.order_type_id
                    LEFT JOIN public.lookup st ON st.id = o.status_id
                    WHERE o.user_id = @UserId

                    UNION ALL

                    SELECT
                        'trade'::text,
                        t.id,
                        COALESCE(i.symbol, 'Trade'),
                        CONCAT(COALESCE(t.side, ''), ' · ', t.quantity::text),
                        t.trade_value,
                        NULL,
                        t.executed_at
                    FROM public.trades t
                    LEFT JOIN public.instruments i ON i.id = t.instrument_id
                    WHERE t.user_id = @UserId

                    UNION ALL

                    SELECT
                        'wallet'::text,
                        wh.id,
                        wh.transaction_type,
                        COALESCE(wh.description, wh.transaction_type),
                        wh.amount,
                        NULL,
                        wh.created_at
                    FROM public.wallet_history wh
                    WHERE wh.user_id = @UserId
                ) activity
                ORDER BY CreatedAt DESC
                LIMIT @Limit;
                """,
                new { UserId = userId, Limit = limit });

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent activity for user {UserId}", userId);
            return new List<UserRecentActivityItemDto>();
        }
    }

    public async Task<List<MarketInstrumentSnapshotDto>> GetMarketSnapshotAsync(int limit = 20)
    {
        if (limit < 1)
            limit = 20;
        if (limit > 100)
            limit = 100;

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<MarketInstrumentSnapshotDto>(
                """
                SELECT
                    i.id AS InstrumentId,
                    i.symbol AS Symbol,
                    i.short_name AS ShortName,
                    i.name AS Name,
                    mq.bid_price AS BidPrice,
                    mq.ask_price AS AskPrice,
                    mq.last_trade_price AS LastTradePrice,
                    mq.quote_timestamp AS QuoteTimestamp
                FROM public.instruments i
                INNER JOIN public.market_quotes mq ON mq.instrument_id = i.id
                LEFT JOIN public.lookup ls ON ls.id = i.status
                WHERE COALESCE(i.is_deleted, false) = false
                  AND mq.is_active = true
                  AND (ls.code = 'active' OR ls.code IS NULL)
                ORDER BY mq.quote_timestamp DESC
                LIMIT @Limit;
                """,
                new { Limit = limit });

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load market snapshot");
            return new List<MarketInstrumentSnapshotDto>();
        }
    }

    public async Task<List<UserTransferRecipientDto>> SearchTransferRecipientsAsync(
        string query,
        int limit = 20,
        int? excludeUserId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<UserTransferRecipientDto>();

        if (limit < 1)
            limit = 20;
        if (limit > 50)
            limit = 50;

        try
        {
            var connection = await GetOpenConnection();
            var pattern = $"%{query.Trim()}%";

            var result = await connection.QueryAsync<UserTransferRecipientDto>(
                """
                SELECT
                    u.id AS UserId,
                    u.username AS Username,
                    u.first_name_en AS FirstNameEn,
                    u.last_name_en AS LastNameEn,
                    w.id AS WalletId
                FROM public.users u
                LEFT JOIN public.wallets w ON w.user_id = u.id
                WHERE COALESCE(u.is_deleted, false) = false
                  AND COALESCE(u.is_locked, false) = false
                  AND (@ExcludeUserId IS NULL OR u.id <> @ExcludeUserId)
                  AND (
                        u.username ILIKE @Pattern
                        OR u.first_name_en ILIKE @Pattern
                        OR u.last_name_en ILIKE @Pattern
                        OR CONCAT(u.first_name_en, ' ', u.last_name_en) ILIKE @Pattern
                  )
                ORDER BY u.username
                LIMIT @Limit;
                """,
                new { Pattern = pattern, Limit = limit, ExcludeUserId = excludeUserId });

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search transfer recipients for query {Query}", query);
            return new List<UserTransferRecipientDto>();
        }
    }
}
