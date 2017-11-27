using System.Drawing;
using CatalogueLibrary.Data;
using CatalogueManager.ItemActivation;
using CatalogueManager.ItemActivation.Emphasis;
using ReusableUIComponents.CommandExecution.AtomicCommands;
using ReusableUIComponents.Icons.IconProvision;

namespace CatalogueManager.CommandExecution.AtomicCommands
{
    public class ExecuteCommandUnpin : BasicUICommandExecution, IAtomicCommand
    {
        private readonly DatabaseEntity _databaseEntity;

        public ExecuteCommandUnpin(IActivateItems activator, DatabaseEntity databaseEntity)
            : base(activator)
        {
            _databaseEntity = databaseEntity;
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            return null;
        }

        public override void Execute()
        {
            base.Execute();

            Activator.RequestItemEmphasis(this, new EmphasiseRequest(_databaseEntity, int.MaxValue) { Pin = false });
        }
    }
}