-- ============================================================================
-- BA DMO N41 — enforce at most one active warehouse occupation per position
-- (D-14 Option A). Historical released occupations remain unrestricted.
-- ============================================================================

CREATE UNIQUE INDEX IF NOT EXISTS uq_warehouse_stock_active_position
    ON warehouse_stock (warehouse_location_id)
    WHERE released_at_utc IS NULL;
