\set ON_ERROR_STOP on

INSERT INTO "Categories" (
    "Id", "Name", "Slug", "MetaTitle", "MetaDescription", "SeoContent",
    "IsPublished", "RobotsIndex", "RobotsFollow")
VALUES (
    '37000000-0000-0000-0000-000000000001',
    'Rehearsal Footwear',
    'rehearsal-footwear',
    'Rehearsal Footwear',
    'Synthetic category for the isolated upgrade and restore rehearsal.',
    'Synthetic rehearsal catalog content.',
    TRUE,
    TRUE,
    TRUE);

INSERT INTO "Products" (
    "Id", "Name", "Slug", "Description", "Price", "Quantity", "CreatedOn",
    "PublishedOn", "IsPublished", "CategoryId", "MetaTitle", "MetaDescription",
    "SeoContent", "RobotsIndex", "RobotsFollow")
VALUES
    (
        '37000000-0000-0000-0000-000000000010',
        'Rehearsal Canvas Bag',
        'rehearsal-canvas-bag',
        'Synthetic simple product created by the historical rehearsal fixture.',
        12.50,
        19,
        '2026-08-19T10:00:00Z',
        '2026-08-19T11:00:00Z',
        TRUE,
        '37000000-0000-0000-0000-000000000001',
        'Rehearsal Canvas Bag',
        'Synthetic simple product created by the historical rehearsal fixture.',
        'Synthetic simple product.',
        TRUE,
        TRUE
    ),
    (
        '37000000-0000-0000-0000-000000000020',
        'Rehearsal Trail Shoe',
        'rehearsal-trail-shoe',
        'Synthetic variant-backed product created by the historical rehearsal fixture.',
        25.00,
        0,
        '2026-08-19T10:05:00Z',
        '2026-08-19T11:05:00Z',
        TRUE,
        '37000000-0000-0000-0000-000000000001',
        'Rehearsal Trail Shoe',
        'Synthetic variant-backed product created by the historical rehearsal fixture.',
        'Synthetic variant-backed product.',
        TRUE,
        TRUE
    );

INSERT INTO "ProductVariants" (
    "Id", "ProductId", "Sku", "SizeScale", "SizeValue", "Price", "Stock", "Color", "IsDefault")
VALUES (
    '37000000-0000-0000-0000-000000000021',
    '37000000-0000-0000-0000-000000000020',
    'REHEARSAL-US10-BLUE',
    11,
    '10',
    31.25,
    7,
    'Blue',
    TRUE);

INSERT INTO "Orders" (
    "Id", "UserId", "Status", "Reference", "TotalAmount", "CreatedOn",
    "ShippingCarrier", "TrackingNumber", "TrackingUrl", "ShippingStatus",
    "ShippedOn", "DeliveredOn", "LastTrackingUpdate", "AdminNote")
VALUES
    (
        '37000000-0000-0000-0000-000000000100',
        :'user_id',
        'Paid',
        'REHEARSAL-LEGACY-001',
        25.00,
        '2026-08-19T12:00:00Z',
        'Synthetic Carrier',
        'TRACK-001',
        NULL,
        'delivered',
        '2026-08-19T13:00:00Z',
        '2026-08-20T15:00:00Z',
        '2026-08-20T15:00:00Z',
        'Synthetic legacy paid label without provider evidence.'
    ),
    (
        '37000000-0000-0000-0000-000000000200',
        :'user_id',
        'Pending',
        'REHEARSAL-LEGACY-002',
        62.50,
        '2026-08-19T12:05:00Z',
        'Synthetic Carrier',
        'TRACK-002',
        NULL,
        'OutForDelivery',
        NULL,
        NULL,
        '2026-08-19T14:00:00Z',
        'Synthetic legacy variant purchase without historical variant identity.'
    );

INSERT INTO "OrderLines" ("Id", "OrderId", "ProductId", "Quantity", "UnitPrice")
VALUES
    (
        '37000000-0000-0000-0000-000000000101',
        '37000000-0000-0000-0000-000000000100',
        '37000000-0000-0000-0000-000000000010',
        2,
        12.50
    ),
    (
        '37000000-0000-0000-0000-000000000201',
        '37000000-0000-0000-0000-000000000200',
        '37000000-0000-0000-0000-000000000020',
        2,
        31.25
    );

INSERT INTO "CheckoutOrderItems" ("Id", "ProductId", "Quantity", "UserId", "CreatedOn")
VALUES
    (
        '37000000-0000-0000-0000-000000000301',
        '37000000-0000-0000-0000-000000000010',
        1,
        :'user_id',
        '2026-08-19T12:10:00Z'
    ),
    (
        '37000000-0000-0000-0000-000000000302',
        '37000000-0000-0000-0000-000000000020',
        3,
        :'user_id',
        '2026-08-19T12:11:00Z'
    );
