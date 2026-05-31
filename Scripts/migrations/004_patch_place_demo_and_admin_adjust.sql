-- Run once in Supabase SQL editor AFTER 003_wallet_freeze_guard.sql.
-- Patches existing functions if they do not already call wallet_freeze_guard_json.
-- Adjust parameter names below if your functions use different names (check with \df+ in psql).

-- ---------- place_demo_order ----------
-- Typical first arg: p_user_id or user_id. Change v_user_param if needed.
DO $patch$
DECLARE
    v_oid oid;
    v_def text;
    v_user_param text := 'p_user_id';
    v_guard_block text;
BEGIN
    SELECT p.oid
    INTO v_oid
    FROM pg_proc p
    JOIN pg_namespace n ON n.oid = p.pronamespace
    WHERE n.nspname = 'public'
      AND p.proname = 'place_demo_order'
    ORDER BY p.oid
    LIMIT 1;

    IF v_oid IS NULL THEN
        RAISE NOTICE 'place_demo_order: function not found — skip';
        RETURN;
    END IF;

    v_def := pg_get_functiondef(v_oid);

    IF v_def ILIKE '%wallet_freeze_guard_json%' THEN
        RAISE NOTICE 'place_demo_order: already patched';
        RETURN;
    END IF;

    -- Discover first argument name if p_user_id is wrong
    SELECT a.attname
    INTO v_user_param
    FROM pg_proc p
    JOIN pg_namespace n ON n.oid = p.pronamespace
    CROSS JOIN LATERAL unnest(p.proargnames) WITH ORDINALITY AS a(attname, ord)
    WHERE p.oid = v_oid
      AND a.ord = 1
      AND a.attname IS NOT NULL
      AND a.attname <> '';

    IF v_user_param IS NULL OR v_user_param = '' THEN
        v_user_param := 'p_user_id';
    END IF;

    v_guard_block := format(
        E'\n    v_wallet_freeze_guard text;\n'
        || E'    v_wallet_freeze_guard := public.wallet_freeze_guard_json(%1$I);\n'
        || E'    IF v_wallet_freeze_guard IS NOT NULL THEN\n'
        || E'        RETURN v_wallet_freeze_guard;\n'
        || E'    END IF;\n',
        v_user_param
    );

    IF v_def ~* '\mDECLARE\M' THEN
        v_def := regexp_replace(
            v_def,
            '(\mDECLARE\M)',
            'DECLARE' || E'\n    v_wallet_freeze_guard text;',
            1
        );
        v_def := regexp_replace(v_def, '(\mBEGIN\M)', 'BEGIN' || v_guard_block, 1);
    ELSE
        v_def := regexp_replace(
            v_def,
            '(\mBEGIN\M)',
            'BEGIN' || E'\n    v_wallet_freeze_guard text;' || v_guard_block,
            1
        );
    END IF;

    EXECUTE v_def;
    RAISE NOTICE 'place_demo_order: patched using user param %', v_user_param;
END;
$patch$;

-- ---------- admin_adjust_wallet ----------
-- Target user is usually the second argument (not the admin).
DO $patch$
DECLARE
    v_oid oid;
    v_def text;
    v_target_param text;
    v_guard_block text;
BEGIN
    SELECT p.oid
    INTO v_oid
    FROM pg_proc p
    JOIN pg_namespace n ON n.oid = p.pronamespace
    WHERE n.nspname = 'public'
      AND p.proname = 'admin_adjust_wallet'
    ORDER BY p.oid
    LIMIT 1;

    IF v_oid IS NULL THEN
        RAISE NOTICE 'admin_adjust_wallet: function not found — skip';
        RETURN;
    END IF;

    v_def := pg_get_functiondef(v_oid);

    IF v_def ILIKE '%wallet_freeze_guard_json%' THEN
        RAISE NOTICE 'admin_adjust_wallet: already patched';
        RETURN;
    END IF;

    SELECT a.attname
    INTO v_target_param
    FROM pg_proc p
    CROSS JOIN LATERAL unnest(p.proargnames) WITH ORDINALITY AS a(attname, ord)
    WHERE p.oid = v_oid
      AND a.ord = 2
      AND a.attname IS NOT NULL
      AND a.attname <> '';

    IF v_target_param IS NULL OR v_target_param = '' THEN
        v_target_param := 'p_target_user_id';
    END IF;

    v_guard_block := format(
        E'\n    v_wallet_freeze_guard text;\n'
        || E'    v_wallet_freeze_guard := public.wallet_freeze_guard_json(%1$I);\n'
        || E'    IF v_wallet_freeze_guard IS NOT NULL THEN\n'
        || E'        RETURN v_wallet_freeze_guard;\n'
        || E'    END IF;\n',
        v_target_param
    );

    IF v_def ~* '\mDECLARE\M' THEN
        v_def := regexp_replace(
            v_def,
            '(\mDECLARE\M)',
            'DECLARE' || E'\n    v_wallet_freeze_guard text;',
            1
        );
        v_def := regexp_replace(v_def, '(\mBEGIN\M)', 'BEGIN' || v_guard_block, 1);
    ELSE
        v_def := regexp_replace(
            v_def,
            '(\mBEGIN\M)',
            'BEGIN' || E'\n    v_wallet_freeze_guard text;' || v_guard_block,
            1
        );
    END IF;

    EXECUTE v_def;
    RAISE NOTICE 'admin_adjust_wallet: patched using target param %', v_target_param;
END;
$patch$;
