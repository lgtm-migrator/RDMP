﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using SixLabors.ImageSharp;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Icons.IconProvision;
using ReusableLibraryCode.Icons.IconProvision;
using SixLabors.ImageSharp.PixelFormats;

namespace Rdmp.Core.CommandExecution.AtomicCommands
{
    public class ExecuteCommandAddFavourite : BasicCommandExecution
    {
        private DatabaseEntity _databaseEntity;

        public ExecuteCommandAddFavourite(IBasicActivateItems activator) : base(activator)
        {
            Weight = 100.1f;
        }

        public ExecuteCommandAddFavourite(IBasicActivateItems activator, DatabaseEntity databaseEntity) : this(activator)
        {
            _databaseEntity = databaseEntity;

            Weight = 100.1f;
        }

        public override string GetCommandName()
        {
            if (_databaseEntity == null)
                return base.GetCommandName();

            return BasicActivator.FavouritesProvider.IsFavourite(_databaseEntity) ? "UnFavourite" : "Favourite";
        }

        public override void Execute()
        {
            base.Execute();

            if (_databaseEntity != null)
            {
                if (BasicActivator.FavouritesProvider.IsFavourite(_databaseEntity))
                    BasicActivator.FavouritesProvider.RemoveFavourite(this, _databaseEntity);
                else
                    BasicActivator.FavouritesProvider.AddFavourite(this, _databaseEntity);
            }
            else
            {
                
                BasicActivator.SelectAnythingThen("Add Favourite",
                    (a) =>
                {
                    if (BasicActivator.FavouritesProvider.IsFavourite(a))
                        Show($"'{a}' is already a Favourite");
                    else
                        BasicActivator.FavouritesProvider.AddFavourite(this, a);
                });
            }

        }

        public override Image<Rgba32> GetImage(IIconProvider iconProvider)
        {
            if (_databaseEntity != null && BasicActivator.FavouritesProvider.IsFavourite(_databaseEntity))
                return Image.Load<Rgba32>(CatalogueIcons.StarHollow);

            return iconProvider.GetImage(RDMPConcept.Favourite, OverlayKind.Add);
        }
    }
}