-- Run against your VoxTrade PostgreSQL database (once).
-- Optional audit fields for admin deactivate/restore.
-- Without delete_at/deleted_by, the API falls back to updating is_deleted only.

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT false;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS delete_at timestamptz NULL;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS deleted_by integer NULL;

COMMENT ON COLUMN public.users.is_deleted IS 'Soft-delete flag; deactivated users cannot sign in';
COMMENT ON COLUMN public.users.delete_at IS 'When the account was deactivated';
COMMENT ON COLUMN public.users.deleted_by IS 'Admin user id who deactivated the account';
