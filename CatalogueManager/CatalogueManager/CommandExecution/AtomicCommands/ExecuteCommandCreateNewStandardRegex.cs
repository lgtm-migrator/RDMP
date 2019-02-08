// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using CatalogueLibrary.Data;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;

namespace CatalogueManager.CommandExecution.AtomicCommands
{
    public class ExecuteCommandCreateNewStandardRegex : BasicUICommandExecution,IAtomicCommand
    {
        public ExecuteCommandCreateNewStandardRegex(IActivateItems activator):base(activator)
        {
            
        }

        public override void Execute()
        {
            var regex = new StandardRegex(Activator.RepositoryLocator.CatalogueRepository);

            Publish(regex);
            Emphasise(regex);
            Activate(regex);
        }

        public override string GetCommandHelp()
        {
            return "Regular Expressions are patterns that match a given text input.  StandardRegex allow a central declaration of a given pattern rather than copying and pasting it everywhere";
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.StandardRegex, OverlayKind.Add);
        }
    }
}