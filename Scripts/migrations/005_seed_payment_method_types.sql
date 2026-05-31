-- Optional seed: common payment method types for the catalog.
-- Run if payment_method is empty.

INSERT INTO public.payment_method (mehtod_name, method_type, status, status_group, is_deleted)
SELECT v.name, v.type, 1, 1, false
FROM (VALUES
    ('Debit Card', 'card'),
    ('Credit Card', 'card'),
    ('Bank Transfer', 'bank'),
    ('PayPal', 'wallet'),
    ('Apple Pay', 'wallet')
) AS v(name, type)
WHERE NOT EXISTS (
    SELECT 1 FROM public.payment_method pm
    WHERE pm.mehtod_name = v.name AND pm.method_type = v.type
);
