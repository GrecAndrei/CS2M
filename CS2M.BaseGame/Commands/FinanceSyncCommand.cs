using CS2M.API.Commands;
using MessagePack;

namespace CS2M.BaseGame.Commands
{
    /// <summary>
    ///     Command to synchronize city finance settings including tax rates and service budgets
    /// </summary>
    [MessagePackObject]
    public class FinanceSyncCommand : CommandBase
    {
        [Key(0)]
        public float[] BudgetSliderValues { get; set; }

        [Key(1)]
        public float[] TaxSliderValues { get; set; }

        [Key(2)]
        public int TransactionNonce { get; set; }

        [Key(3)]
        public bool RequestOnly { get; set; }

        public override bool Validate() => true;
    }
}
