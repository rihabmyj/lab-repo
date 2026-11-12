# ECommerce.Web

**Type:** Blazor Web App (interactive server render mode)
**Depends on:** ECommerce.ServiceDefaults, **calls the Gateway**

The web frontend. It is just another client of the system — it goes through the Gateway
exactly like an external caller, never directly to Catalog or Ordering.

## Files

| File | Responsibility |
|---|---|
| `Services/CatalogApiClient.cs` | Typed client → `/catalog/...` via the gateway. `ProductDto`. |
| `Services/OrderingApiClient.cs` | Typed client → `/ordering/...` via the gateway. Order DTOs. |
| `Components/Pages/Home.razor` | Landing page with links to Catalog and Orders. |
| `Components/Pages/Products.razor` | Catalog list + in-memory cart + place-order form. |
| `Components/Pages/Orders.razor` | Order history with a refresh button. |
| `Components/Layout/NavMenu.razor` | Navigation: Catalog / Orders. |
| `wwwroot/app.css` | Design tokens (`:root`), dark theme, base typography, focus ring, Bootstrap mapped onto the tokens. |
| `Components/**/*.razor.css` | Scoped layout of each component (sidebar, top bar, card grids, product table, status badge). |
| `Program.cs` | Registers the two typed clients pointing at `https+http://gateway`. |

## Pages / flow

1. **Catalog** (`/products`) — lists products from Catalog, lets you add items to a cart,
   enter a customer name, and place an order (POST to Ordering).
2. **Orders** (`/orders`) — lists orders from Ordering.

## Patterns

- **Typed `HttpClient`s** registered with `AddHttpClient<T>` — they inherit service discovery
  and resilience from ServiceDefaults.
- **UI as a gateway client** — both clients target `https+http://gateway`, so the UI is
  decoupled from the internal service topology, same as any other consumer.

## Styling

Bootstrap stays loaded as is (`wwwroot/lib`); the look of the app is layered on top of it,
without CDN, downloaded font or JavaScript.

- **Design tokens** — colors, radii, shadows, spacing, typography and motion are custom
  properties defined once on `:root` in `wwwroot/app.css`. Bootstrap variables (`--bs-body-bg`,
  `--bs-card-*`, `--bs-table-*`, `--bs-btn-*`, `--bs-alert-*`…) are mapped onto these tokens,
  and the scoped `*.razor.css` files only use `var(--…)`: no hard-coded color elsewhere.
- **Dark theme** — `@media (prefers-color-scheme: dark)` redefines the color and shadow tokens
  only; every surface (page, sidebar, cards, table, fields, alerts, badges) follows.
- **Typography** — system font stack, fluid heading sizes with `clamp()`, line height 1.5.
- **Layout** — sober sidebar with an accent bar on the active link, content centered with a
  maximum width, Home cards and the order list in `repeat(auto-fill, minmax(…))` grids, the
  product table in its own horizontally scrollable container (no page scroll at 320 px).
- **Interaction** — 150 ms transitions on hover and focus (disabled with
  `prefers-reduced-motion: reduce`), a `:focus-visible` outline ring on links, buttons and fields.
- **Order status** — the badge carries `data-status` and gets a semantic color
  (Pending, Confirmed, Shipped, Cancelled).

## Notes

The default Blazor template's `Counter` and `Weather` demo pages were removed.
