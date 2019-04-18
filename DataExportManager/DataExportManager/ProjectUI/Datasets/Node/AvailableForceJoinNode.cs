// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using CatalogueLibrary.Data;
using CatalogueLibrary.Repositories;
using DataExportLibrary.Data.DataTables;
using ReusableLibraryCode.Annotations;

namespace DataExportManager.ProjectUI.Datasets.Node
{
    internal class AvailableForceJoinNode : IMasqueradeAs
    {
        [CanBeNull]
        public SelectedDataSetsForcedJoin ForcedJoin { get; set; }

        public TableInfo TableInfo { get; set; }
        public JoinInfo[] JoinInfos { get; set; }
        public bool IsMandatory { get; set; }

        /// <summary>
        /// The table will be in the query if it IsMandatory (becaues of the columns the user has selected) or is explicitly picked for inclusion by the user (ForcedJoin)
        /// </summary>
        public bool IsIncludedInQuery { get { return ForcedJoin != null || IsMandatory; }}

        public AvailableForceJoinNode(TableInfo tableInfo, bool isMandatory)
        {
            TableInfo = tableInfo;
            IsMandatory = isMandatory;
        }

        public object MasqueradingAs()
        {
            return TableInfo;
        }

        public override string ToString()
        {
            return TableInfo.ToString();
        }

        #region Equality Members
        protected bool Equals(AvailableForceJoinNode other)
        {
            return TableInfo.Equals(other.TableInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AvailableForceJoinNode) obj);
        }

        public override int GetHashCode()
        {
            return TableInfo.GetHashCode();
        }

        #endregion

        public void FindJoinsBetween(HashSet<AvailableForceJoinNode> otherNodes)
        {
            var mycols = TableInfo.ColumnInfos;

            List<JoinInfo> foundJoinInfos = new List<JoinInfo>();

            foreach (AvailableForceJoinNode otherNode in otherNodes)
            {
                //don't look for self joins
                if(Equals(otherNode , this))
                    continue;

                var theirCols = otherNode.TableInfo.ColumnInfos;
                foundJoinInfos.AddRange(TableInfo.CatalogueRepository.JoinManager.GetAllJoinInfosBetweenColumnInfoSets(mycols, theirCols));
            }

            JoinInfos = foundJoinInfos.ToArray();
        }
    }
}
