﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;

namespace Rdmp.Core.Reports
{
    /// <summary>
    /// Thrown when a problem is encountered parsing a template during <see cref="CustomMetadataReport"/> execution
    /// </summary>
    public class CustomMetadataReportException : Exception
    {
        /// <summary>
        /// The line number in the template that the error occurred (first line of file is 1)
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Tells the user there is a problem with a template being used in <see cref="CustomMetadataReport"/>
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="lineNumber">The line number in the template that the error occurred (first line of file is 1)</param>
        public CustomMetadataReportException(string msg,int lineNumber) : base(msg)
        {
            LineNumber = lineNumber;
        }

        public CustomMetadataReportException(string msg, Exception inner,int lineNumber):base(msg,inner)
        {
            LineNumber = lineNumber;
        }
        
    }
}