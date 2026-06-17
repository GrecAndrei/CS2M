using CS2M.API.Commands;

namespace CS2M.Commands.Data.Internal
{
    public class PreconditionsSuccessCommand : CommandBase
    {
        public override bool Validate() => true;
    }
}
