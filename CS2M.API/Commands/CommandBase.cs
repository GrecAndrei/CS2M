namespace CS2M.API.Commands
{
    /// <summary>
    ///     A base command that all other commands in this mod should extend.
    ///     Provides serialization via inheriting from BaseCommand with MessagePack attributes.
    /// </summary>
    public abstract class CommandBase : BaseCommand
    {
        /// <summary>
        ///     Override to specify custom command type name (defaults to class name)
        /// </summary>
        public override string CommandType => GetCommandType();
    }
}
