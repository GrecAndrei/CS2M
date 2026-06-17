using System;
using System.Linq;
using CS2M.BaseGame.Commands;
using CS2M.Helpers;
using Unity.Entities;

namespace CS2M.BaseGame
{
    internal static class FinanceSyncService
    {
        public static bool TryApplyFinance(FinanceSyncCommand command)
        {
            if (command == null) return false;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return false;

            try
            {
                // Retrieve systems by string name to prevent version/assembly binding issues
                var taxSystem = world.Systems.FirstOrDefault(s => s.GetType().Name == "TaxSystem");
                var financeSystem = world.Systems.FirstOrDefault(s => s.GetType().Name == "CityFinanceSystem");

                if (taxSystem != null && command.TaxSliderValues != null)
                {
                    // Reflectively set tax rates
                    System.Reflection.FieldInfo ratesField = taxSystem.GetType().GetField("m_TaxRates", ReflectionHelper.AllAccessFlags) 
                        ?? taxSystem.GetType().GetField("_taxRates", ReflectionHelper.AllAccessFlags);

                    if (ratesField != null)
                    {
                        ratesField.SetValue(taxSystem, command.TaxSliderValues);
                        Log.Debug($"FinanceSyncService: Updated tax rates.");
                    }
                }

                if (financeSystem != null && command.BudgetSliderValues != null)
                {
                    // Reflectively set service budgets
                    System.Reflection.FieldInfo budgetsField = financeSystem.GetType().GetField("m_ServiceBudgets", ReflectionHelper.AllAccessFlags)
                        ?? financeSystem.GetType().GetField("_serviceBudgets", ReflectionHelper.AllAccessFlags);

                    if (budgetsField != null)
                    {
                        budgetsField.SetValue(financeSystem, command.BudgetSliderValues);
                        Log.Debug($"FinanceSyncService: Updated service budgets.");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"FinanceSyncService failed to apply: {ex.Message}");
                return false;
            }
        }
    }
}
