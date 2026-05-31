-- Optional: atomic P2P transfer in PostgreSQL (C# WalletTransferService already handles this).
-- Run only if you want DB-level transfers callable outside the API.

CREATE OR REPLACE FUNCTION public.transfer_wallet_p2p(
    p_from_user_id integer,
    p_to_user_id integer,
    p_amount numeric,
    p_description text DEFAULT NULL
)
RETURNS text
LANGUAGE plpgsql
AS $$
DECLARE
    v_guard text;
    v_sender public.wallets%ROWTYPE;
    v_receiver public.wallets%ROWTYPE;
    v_sender_username text;
    v_receiver_username text;
    v_note text;
    v_sender_desc text;
    v_receiver_desc text;
BEGIN
    IF p_from_user_id IS NULL OR p_to_user_id IS NULL OR p_from_user_id = p_to_user_id THEN
        RETURN json_build_object('success', false, 'message', 'Invalid sender or recipient')::text;
    END IF;

    IF p_amount IS NULL OR p_amount <= 0 THEN
        RETURN json_build_object('success', false, 'message', 'Amount must be greater than zero')::text;
    END IF;

    v_guard := public.wallet_freeze_guard_json(p_from_user_id);
    IF v_guard IS NOT NULL THEN RETURN v_guard; END IF;

    v_guard := public.wallet_freeze_guard_json(p_to_user_id);
    IF v_guard IS NOT NULL THEN
        RETURN json_build_object(
            'success', false,
            'message', 'Recipient wallet is frozen and cannot receive transfers.'
        )::text;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public.users
        WHERE id = p_from_user_id
          AND COALESCE(is_deleted, false) = false
          AND COALESCE(is_locked, false) = false
    ) THEN
        RETURN json_build_object('success', false, 'message', 'Sender account not found or is not active')::text;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public.users
        WHERE id = p_to_user_id
          AND COALESCE(is_deleted, false) = false
          AND COALESCE(is_locked, false) = false
    ) THEN
        RETURN json_build_object('success', false, 'message', 'Recipient account not found or is not active')::text;
    END IF;

    SELECT * INTO v_sender FROM public.wallets WHERE user_id = p_from_user_id FOR UPDATE;
    IF NOT FOUND THEN
        RETURN json_build_object('success', false, 'message', 'Sender wallet not found')::text;
    END IF;

    SELECT * INTO v_receiver FROM public.wallets WHERE user_id = p_to_user_id FOR UPDATE;
    IF NOT FOUND THEN
        RETURN json_build_object('success', false, 'message', 'Recipient wallet not found')::text;
    END IF;

    IF v_sender.available_balance < p_amount THEN
        RETURN json_build_object(
            'success', false,
            'message', format('Insufficient available balance. Available: %s', v_sender.available_balance)
        )::text;
    END IF;

    SELECT username INTO v_sender_username FROM public.users WHERE id = p_from_user_id;
    SELECT username INTO v_receiver_username FROM public.users WHERE id = p_to_user_id;

    v_note := NULLIF(btrim(COALESCE(p_description, '')), '');
    v_sender_desc := COALESCE(v_note, 'Transfer to ' || COALESCE(v_receiver_username, 'user ' || p_to_user_id::text));
    v_receiver_desc := COALESCE(v_note, 'Transfer from ' || COALESCE(v_sender_username, 'user ' || p_from_user_id::text));

    UPDATE public.wallets
    SET balance = balance - p_amount,
        available_balance = available_balance - p_amount,
        updated_at = NOW()
    WHERE id = v_sender.id;

    UPDATE public.wallets
    SET balance = balance + p_amount,
        available_balance = available_balance + p_amount,
        updated_at = NOW()
    WHERE id = v_receiver.id;

    INSERT INTO public.wallet_history (
        wallet_id, user_id, transaction_type, amount,
        balance_before, balance_after,
        available_before, available_after,
        reserved_before, reserved_after,
        description, created_at
    ) VALUES (
        v_sender.id, p_from_user_id, 'transfer_out', p_amount,
        v_sender.balance, v_sender.balance - p_amount,
        v_sender.available_balance, v_sender.available_balance - p_amount,
        v_sender.reserved_balance, v_sender.reserved_balance,
        v_sender_desc, NOW()
    );

    INSERT INTO public.wallet_history (
        wallet_id, user_id, transaction_type, amount,
        balance_before, balance_after,
        available_before, available_after,
        reserved_before, reserved_after,
        description, created_at
    ) VALUES (
        v_receiver.id, p_to_user_id, 'transfer_in', p_amount,
        v_receiver.balance, v_receiver.balance + p_amount,
        v_receiver.available_balance, v_receiver.available_balance + p_amount,
        v_receiver.reserved_balance, v_receiver.reserved_balance,
        v_receiver_desc, NOW()
    );

    RETURN json_build_object(
        'success', true,
        'message', 'Transfer completed successfully.',
        'fromUserId', p_from_user_id,
        'toUserId', p_to_user_id,
        'amount', p_amount,
        'senderBalanceAfter', v_sender.balance - p_amount,
        'senderAvailableBalanceAfter', v_sender.available_balance - p_amount,
        'receiverBalanceAfter', v_receiver.balance + p_amount,
        'receiverAvailableBalanceAfter', v_receiver.available_balance + p_amount
    )::text;
END;
$$;

COMMENT ON FUNCTION public.transfer_wallet_p2p(integer, integer, numeric, text) IS
    'P2P transfer: deduct sender balance/available, credit receiver, write wallet_history.';
