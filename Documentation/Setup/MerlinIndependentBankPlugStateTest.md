# Merlin Independent Bank Plug-State Test

## Apply

1. Exit Play mode.
2. Pull `agent/merlin-engine-assembly`.
3. Wait for Unity compilation and clear the Console.
4. Run **Hanger 51 > Merlin Condition > 24 - Preserve Untouched Bank Spark Plugs**.
5. Run **Hanger 51 > Merlin Condition > 25 - Validate Independent Bank Spark Plugs**.
6. Expect `Merlin Condition Step 25 passed`.

## Purchased-engine regression

1. Enter Play mode.
2. Purchase a complete V-1650 assembly.
3. Install it in the P-51 or place it where maintenance is accessible.
4. Leave all 12 spark plugs installed on the right bank.
5. Remove all 12 spark plugs from the left bank.
6. Loosen all 6 left-bank cover bolts.
7. Remove the left cylinder cover.
8. Confirm all 12 right-bank spark plugs remain visible and installed.
9. Equip the removed or replacement cylinder cover.
10. Hold `E` to place the left cover.
11. Confirm the right-bank spark plugs remain visible immediately after cover placement.
12. Tighten the 6 left-bank bolts one at a time.
13. After every bolt, confirm the right-bank spark plugs remain visible.
14. After the sixth bolt, install the 12 left-bank plugs.
15. Press `X` on plugs from both banks and verify their installed state and condition.

## Reverse-bank test

Repeat the workflow with the right bank removed while the left bank remains untouched. The left bank's 12 plugs must remain installed throughout right-cover placement and bolt tightening.

## Expected behavior

- Removing a bank requires that bank's plugs and bolts only.
- Reinstalling one cover never clears spark-plug state on the opposite secured bank.
- Tightening bolts on one bank never clears spark-plug state on the opposite secured bank.
- The serviced bank remains empty until its own cover is fully secured and its plugs are reinstalled.
- Spark-plug condition on the untouched bank is preserved rather than reset.
