create table if not exists users (
    id              uuid        primary key,
    email           text        not null unique,
    password_hash   text        not null,
    display_name    text        not null,
    role            smallint    not null,
    created_at      timestamptz not null
);

create table if not exists customers (
    id              uuid        primary key,
    company         text        not null,
    contact_name    text        not null,
    email           text        not null,
    phone           text,
    status          smallint    not null,
    owner_id        uuid        not null references users(id),
    created_at      timestamptz not null,
    updated_at      timestamptz not null
);

create index if not exists ix_customers_status on customers(status);

create table if not exists products (
    id              uuid           primary key,
    sku             text           not null unique,
    name            text           not null,
    category        smallint       not null,
    brand           text           not null,
    price           numeric(18,2)  not null,
    stock_on_hand   int            not null,
    -- Bumped on every update; the conditional decrement at order-confirm uses it.
    row_version     int            not null,
    created_at      timestamptz    not null,
    updated_at      timestamptz    not null
);

create index if not exists ix_products_category on products(category);

-- Order numbers are human-readable; sourced from this sequence.
create sequence if not exists order_number_seq start 1;

create table if not exists orders (
    id              uuid           primary key,
    number          text           not null unique,
    customer_id     uuid           not null references customers(id),
    status          smallint       not null,
    subtotal        numeric(18,2)  not null,
    tax             numeric(18,2)  not null,
    total           numeric(18,2)  not null,
    owner_id        uuid           not null references users(id),
    created_at      timestamptz    not null,
    updated_at      timestamptz    not null
);

create index if not exists ix_orders_status on orders(status);
create index if not exists ix_orders_customer on orders(customer_id);

create table if not exists order_items (
    id                    uuid           primary key,
    order_id              uuid           not null references orders(id) on delete cascade,
    product_id            uuid           not null references products(id),
    quantity              int            not null,
    unit_price_snapshot   numeric(18,2)  not null
);

create index if not exists ix_order_items_order on order_items(order_id);
