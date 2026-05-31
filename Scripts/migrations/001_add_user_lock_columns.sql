-- Run against your VoxTrade PostgreSQL database (once).
-- Fixes: 42703 column u.IsLocked does not exist

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS is_locked boolean NOT NULL DEFAULT false;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS locked_at timestamptz NULL;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS locked_by integer NULL;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS lock_reason text NULL;

COMMENT ON COLUMN public.users.is_locked IS 'When true, user cannot sign in';
COMMENT ON COLUMN public.users.lock_reason IS 'Optional reason shown on login denial';
