# Jellyfin Paywall

[Português](README.md)

A Jellyfin plugin that only opens the library to people who paid. It handles one-off payments
and subscriptions, and it is not married to any payment provider.

## Layout

    src/Paywall.Domain          access rules: plan, grant, order. No dependencies at all.
    src/Paywall.Application     use cases and ports (payment, storage, enforcement).
    src/Paywall.Infrastructure  the plugin's own SQLite and the payment adapters.
    src/Jellyfin.Plugin.Paywall composition root: plugin, endpoints, scheduled task.

Dependencies point inwards, so swapping the payment provider, the database or even the media
server means writing an adapter rather than touching a rule.

## Adding a payment method

Implement `IPaymentProvider` and register it in `PaywallServiceRegistrator`. The interface asks
for two things: open a charge and translate the webhook. Providers that can end a recurrence
also implement `ISupportsSubscriptionCancellation`.

Two Pix providers ship with it.

`manual-pix` builds the copy-and-paste code in the Brazilian BR Code standard straight from
your own key, with no middleman. It has no way of learning that the money arrived, so an
administrator releases access by hand.

`asaas` does both single charges and recurring Pix subscriptions, confirmed automatically by
webhook. It needs an API key and a webhook token — without the token the provider does not
even show up, because anyone could forge an approved payment. Asaas requires a payer tax id,
which you set once in the configuration instead of asking every user.

The charge a recurrence raises on its own each month arrives with no order opened here. In that
case the subscriber is found by the recurrence id and a renewal order is created.

Three crypto providers ship as well: `btcpay` for your own BTCPay Server instance, `opennode`
for hosted Bitcoin and Lightning, and `nowpayments` for hundreds of coins behind a hosted
checkout. None of them serves subscriptions, because crypto has no direct debit: nobody can
pull a payment from the user every month. In crypto a "monthly" plan is a 30-day one-off that
the user renews by paying again, and unused days are never lost.

Each one signs its webhook differently, and that stays inside the adapter: BTCPay uses
HMAC-SHA256 over the raw body, OpenNode signs only the charge id with the API key itself and
notifies in form encoding, and NOWPayments uses HMAC-SHA512 over the JSON with keys sorted
alphabetically.

A subscription cancelled at the provider does not cut access: it releases the recurrence and the
user keeps what they already paid for. That is settled straight on the grant, found by the
recurrence id, and never goes through an order — cancelling is not a charge.

## How enforcement works

The core decides allow or deny; `JellyfinAccessEnforcer` applies it through `UpdatePolicyAsync`,
the same path the dashboard uses. Two modes: disable the account, which drops the open session
immediately, or hide the paid libraries and leave only the free ones.

Access is re-evaluated when an account is created, on every session start, and hourly by the
scheduled task. The task alone would leave a brand new account, or a just-expired plan, open
until the next pass.

Revoking cuts immediately and ignores the grace period, which exists for late payment rather
than for someone cut off on purpose. And turning the plugin off gives everyone their access
back on the next evaluation: otherwise disabling it would lock users out permanently.

## What the user sees

A checkout failure reaches the user as one generic sentence; the detail, including the
provider's raw response, stays in the server log. Without that, the payment provider's refusal
would be handed verbatim to the person trying to pay. The admin panel still gets the full
message, because that is where it is worth anything.

User endpoints use bare `[Authorize]`, which falls back to Jellyfin's default policy; admin ones
use the `Policies.RequiresElevation` constant. No policy name is a loose string, so a typo
becomes a compile error instead of a 500 in production.

Payment notifications are handled one at a time. Asaas sends two events for the same charge, and
in parallel both would pass the pending check before either wrote, crediting twice the period.

## Database

The plugin opens its own `paywall.db`, separate from `jellyfin.db`. The server owns its schema
and migrates it on every release, and billing data cannot be held hostage to that.

Migrations are a list in `PaywallDatabase`, applied in order from the version recorded in the
file, each inside its own transaction. The list only grows at the end: editing a script that has
already run would have no effect on anyone who migrated.

Orders and grants store the terms that were sold — plan, price and duration — instead of looking
the catalogue up at release time. Without that, renaming a plan after checkout would let a user
pay and never receive access, with the webhook failing forever.

## User portal

Someone without access opens `/Paywall/Portal`, picks a plan, gets the Pix or crypto charge and
watches the page release itself once payment lands. It is a page of its own because a plugin
cannot inject interface into the Jellyfin web client.

## Configuration

Everything lives in the dashboard, under Dashboard > Plugins > Paywall: plans, keys, grace
period and enforcement mode. The page also shows the exact URL to register in the Asaas and
BTCPay webhooks, and lists subscribers with each one's standing, which is where a manual Pix
gets confirmed.

## Build

    ./build.sh

Copy `artifacts/Paywall_1.0.0.0/` into Jellyfin's `plugins/` folder and restart.
