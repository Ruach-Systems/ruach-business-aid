# Money and quantity entry

Money and stock/sales quantity fields share `NumericInput`, through `MoneyInput`
and `QuantityInput`. This changes client input and display only. Existing centavo
storage, whole-item rules, API validation, and synchronization behavior remain.
The existing RUACH semantic error colors are used; see the workspace's
`ruach-brand-guide` for the authoritative visual guidance.

- New price, cost, and expense fields start empty with a `0.00` placeholder; edits retain saved amounts, including zero.
- Money and quantity placeholders disappear while focused and return when an empty field loses focus.
- Save remains clickable to focus the first invalid control in that form; invalid submission is blocked.
- Money uses a decimal keyboard hint, accepts a decimal point and up to two
  decimal places, and formats to two places on blur without rounding a draft.
- Pasted PHP values such as `₱1,250.50` are accepted. Commas must be three-digit
  groups; ambiguous comma-decimal formats and scientific notation are rejected.
- New received-stock and low-stock fields start empty with a `0` placeholder. Existing counts remain visible. Unused sales quantities appear empty and mean zero; clearing a sales quantity removes that item from the sale. Required stock fields must still be explicitly entered.
- Quantities use a numeric keyboard hint and require whole digits. Selling price
  starts at ₱0.01; cost and expense allow zero. Received stock starts at one;
  counted stock, sale quantities, and low-stock thresholds allow zero.
- Existing limits remain ₱100,000,000 per amount, 1,000,000 per quantity, and
  500 different items per sale. These are technical limits, not suggested values.
- Invalid drafts stay visible, have an associated inline error, block Save, and
  do not enter the numeric model. Affected previews show an em dash rather than
  a stale total. Form handlers also guard against programmatic submission.
- Sales steppers stop at their limits. Invalid rows stay visible while searching.
- Conflict review renders both local and server centavos as PHP amounts.

## Focused verification

Publish the local PWA before browser tests; the fixture uses synthetic accounts,
mock API routes, and an isolated browser context. It does not use production data.

```powershell
dotnet publish src/Ruach.BusinessAid.Client -c Release -o artifacts/pwa
$env:MASHAL_BROWSER_TESTS = 'true'
dotnet test tests/Ruach.BusinessAid.Tests -c Release --filter "FullyQualifiedName~ComponentTests|FullyQualifiedName~ClientLogicTests|FullyQualifiedName~MobileNumericEntryRejectsInvalidDraftsAndSavesExactCentavos"
```

The browser check requires the repository's Playwright Chromium runtime. Keyboard
hints are checked automatically; real Android/iPhone keyboard appearance requires
a device check.

## Manual checklist (local test workspace)

From the `ruach-business-aid` repository, start the development client:

```powershell
dotnet run --project src/Ruach.BusinessAid.Client --launch-profile Client
```

Open `http://localhost:5173`. Use a local disposable workspace (the development
sign-in screen offers **Continue locally**), or your existing local test setup.
The development-only workspace stores data in that browser and does not require
the production API. Do not reset existing browser data to prepare this check.

1. **New item:** set selling price to `35`, unit cost to `10`. Profit is ₱25.00.
   Clear the price or enter `12.345`: an inline error appears, profit becomes `—`,
   and Save focuses the invalid price without saving. Change to `40`: profit becomes ₱30.00.
2. **Money:** paste `₱1,250.50`, then leave the field. It displays `1250.50`.
   Try `1e3`, `-1`, `1,25`, and `100000000.01`; each is rejected without silently
   changing the entry. Zero is valid for cost/expense, but not selling price.
3. **Sales:** enter quantity `2` for a ₱35 item: total is ₱70.00. Try `1.5`, blank,
   or `1000001`: Save is blocked and totals show `—`. Search for another name:
   the invalid row stays available to correct. Return to `2`, save, and verify
   the history amount. At zero, `−` is disabled; at 1,000,000, `+` is disabled.
   Do not save the large boundary-test quantity.
4. **Stock:** received quantity `0` is invalid; counted stock `0` and low-stock
   warning `0` are valid. Fractions and values over 1,000,000 are invalid in all
   quantity fields. Invalid received quantity hides its stock-value preview.
5. **Expenses:** save a small test expense. Edit its amount, enter invalid text,
   then Cancel edit. The add form resets cleanly and can accept a valid amount.
6. **Conflict review:** if a conflict is already available in a disposable test
   workspace, verify amounts appear as pesos on both sides (125050 centavos =
   ₱1,250.50). No need to create a conflict in real business records; the focused
   component test covers this rendering.
7. **Phone:** check decimal/numeric keyboards and readable inline errors. Check
   both normal and compact density. Keyboard/browser-control layout redesign is
   outside this input-correctness change.

Source changes are not automatically deployed. Run this checklist against a local
build containing the changes, not the currently deployed website.
