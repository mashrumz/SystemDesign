-- GoPuff seed data
-- Covers NYC metro area, Chicago, and Los Angeles.
-- Tables are created by EF Core EnsureCreated() when services start.
-- Run this script manually (or via docker exec) AFTER services have started once.
-- All inserts use ON CONFLICT DO NOTHING — re-running is safe.

-- ── Fulfilment Centres ───────────────────────────────────────────────────────
INSERT INTO fulfillment_centres (id, name, lat, lon) VALUES
  (1, 'Manhattan FC',         40.7580,  -73.9855),
  (2, 'Brooklyn FC',          40.6782,  -73.9442),
  (3, 'Queens FC',            40.7282,  -73.7949),
  (4, 'Bronx FC',             40.8448,  -73.8648),
  (5, 'Chicago Loop FC',      41.8827,  -87.6233),
  (6, 'Chicago Lincoln Park', 41.9216,  -87.6533),
  (7, 'LA Downtown FC',       34.0522, -118.2437),
  (8, 'LA Santa Monica FC',   34.0195, -118.4912)
ON CONFLICT (id) DO NOTHING;

-- Advance sequence past seeded IDs so new FCs don't collide
SELECT setval(
  pg_get_serial_sequence('fulfillment_centres', 'id'),
  (SELECT MAX(id) FROM fulfillment_centres)
);

-- ── Items ────────────────────────────────────────────────────────────────────
INSERT INTO items (id, name) VALUES
  (1,  'Water Bottle (1L)'),
  (2,  'Chips (Lay''s Original)'),
  (3,  'Red Bull (250ml)'),
  (4,  'Beer (Heineken 6-pack)'),
  (5,  'Tylenol Extra Strength'),
  (6,  'White Bread Loaf'),
  (7,  'Eggs (12-pack)'),
  (8,  'Milk (1 gallon)'),
  (9,  'Ice Cream (Ben & Jerry''s)'),
  (10, 'Cup Noodles Ramen')
ON CONFLICT (id) DO NOTHING;

SELECT setval(
  pg_get_serial_sequence('items', 'id'),
  (SELECT MAX(id) FROM items)
);

-- ── Inventory (item_id, fc_id, quantity) ─────────────────────────────────────
INSERT INTO inventories (item_id, fc_id, quantity) VALUES
  -- Manhattan FC (1) — full range
  (1,1,500),(2,1,200),(3,1,300),(4,1, 50),
  (5,1,100),(6,1, 80),(7,1, 60),(8,1, 40),
  (9,1, 30),(10,1,150),
  -- Brooklyn FC (2)
  (1,2,400),(2,2,180),(3,2,250),(4,2, 40),
  (5,2, 80),(6,2, 70),(7,2, 50),(8,2, 35),
  (9,2, 20),(10,2,120),
  -- Queens FC (3) — no item 3 or 9
  (1,3,300),(2,3,150),(4,3, 30),
  (5,3, 60),(6,3, 90),(7,3, 70),(8,3, 50),
  (10,3, 90),
  -- Bronx FC (4) — no item 4 or 8
  (1,4,200),(2,4,100),(3,4,100),
  (5,4, 40),(6,4, 50),(7,4, 40),
  (9,4, 10),(10,4, 80),
  -- Chicago Loop FC (5) — full range
  (1,5,450),(2,5,200),(3,5,280),(4,5, 60),
  (5,5, 90),(6,5, 75),(7,5, 55),(8,5, 45),
  (9,5, 25),(10,5,140),
  -- Chicago Lincoln Park FC (6) — no item 3
  (1,6,350),(2,6,160),(4,6, 45),
  (5,6, 70),(6,6, 65),(7,6, 48),(8,6, 38),
  (9,6, 18),(10,6,110),
  -- LA Downtown FC (7) — full range
  (1,7,480),(2,7,210),(3,7,310),(4,7, 55),
  (5,7, 95),(6,7, 82),(7,7, 62),(8,7, 42),
  (9,7, 32),(10,7,160),
  -- LA Santa Monica FC (8) — no item 7
  (1,8,320),(2,8,140),(3,8,190),(4,8, 35),
  (5,8, 65),(6,8, 55),(8,8, 30),
  (9,8, 22),(10,8,100)
ON CONFLICT (item_id, fc_id) DO NOTHING;
