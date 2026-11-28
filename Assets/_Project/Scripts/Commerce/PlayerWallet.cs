using System;
using UnityEngine;

namespace Hanger51.Commerce
{
    [DisallowMultipleComponent]
    public sealed class PlayerWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingBalance = 250000;
        [SerializeField, Min(0)] private int currentBalance;

        public event Action<int> BalanceChanged;

        public int CurrentBalance => currentBalance;
        public int StartingBalance => startingBalance;
        public string FormattedBalance => $"${currentBalance:N0}";

        private void Awake()
        {
            currentBalance = Mathf.Max(0, startingBalance);
        }

        public void ConfigureStartingBalance(int configuredBalance)
        {
            startingBalance = Mathf.Max(0, configuredBalance);
            currentBalance = startingBalance;
            BalanceChanged?.Invoke(currentBalance);
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && currentBalance >= amount;
        }

        public bool TrySpend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (!CanAfford(amount))
            {
                return false;
            }

            currentBalance -= amount;
            BalanceChanged?.Invoke(currentBalance);
            return true;
        }

        public void AddFunds(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            currentBalance += amount;
            BalanceChanged?.Invoke(currentBalance);
        }

        private void OnValidate()
        {
            startingBalance = Mathf.Max(0, startingBalance);
            currentBalance = Mathf.Max(0, currentBalance);
        }
    }
}
