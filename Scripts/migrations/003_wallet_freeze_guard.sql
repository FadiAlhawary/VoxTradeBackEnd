-- Wallet freeze enforcement at the database layer (recommended).
-- Frozen wallet: wallets.status = false
--
-- C# already checks via WalletFreezeGuard before place_demo_order and admin_adjust_wallet.
-- Add the guard below inside each function that moves funds or places orders.

-- Reusable helper: returns JSON text to RETURN early, or NULL if wallet is not frozen.
CREATE OR REPLACE FUNCTION public.wallet_freeze_guard_json(p_user_id integer)
RETURNS text
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    v_reason text;
BEGIN
    SELECT w.freeze_reason
    INTO v_reason
    FROM public.wallets w
    WHERE w.user_id = p_user_id
      AND COALESCE(w.status, true) = false
    LIMIT 1;

    IF NOT FOUND THEN
        RETURN NULL;
    END IF;

    IF v_reason IS NULL OR btrim(v_reason) = '' THEN
        RETURN json_build_object(
            'success', false,
            'message', 'Wallet is frozen. Trading and fund movements are disabled.'
        )::text;
    END IF;

    RETURN json_build_object(
        'success', false,
        'message', 'Wallet is frozen: ' || btrim(v_reason)
    )::text;
END;
$$;

COMMENT ON FUNCTION public.wallet_freeze_guard_json(integer) IS
    'Returns JSON error text when user wallet is frozen (status=false), else NULL.';

-- ---------------------------------------------------------------------------
-- Patch your existing functions manually (parameter names may differ):
--
-- place_demo_order — at the start, after BEGIN:
--   v_guard := public.wallet_freeze_guard_json(p_user_id);  -- use your user id param
--   IF v_guard IS NOT NULL THEN RETURN v_guard; END IF;
--
-- admin_adjust_wallet — at the start:
--   v_guard := public.wallet_freeze_guard_json(p_target_user_id);  -- target user, not admin
--   IF v_guard IS NOT NULL THEN RETURN v_guard; END IF;
--
-- Any P2P / transfer function — same pattern with the sender or affected user id.
-- ---------------------------------------------------------------------------
