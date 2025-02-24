﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Moq;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.Core.DataExport.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Common;

namespace Rdmp.Core.Tests.DataExport.Data
{
    internal class ExtractableCohortAuditLogBuilderTests : UnitTests
    {
        [Test]
        public void AuditLogReFetch_FileInfo()
        {
            var builder = new ExtractableCohortAuditLogBuilder();
            
            var fi = new FileInfo("durdur.txt");
            var desc = builder.GetDescription(fi);

            var moqCohort = Mock.Of<IExtractableCohort>(e => e.AuditLog == desc);
            var fi2 = builder.GetObjectIfAny(moqCohort, RepositoryLocator);

            Assert.IsNotNull(fi2);
            Assert.IsInstanceOf<FileInfo>(fi2);
            Assert.AreEqual(fi.FullName, ((FileInfo)fi2).FullName);
        }


        [Test]
        public void AuditLogReFetch_CohortIdentificationConfiguration()
        {
            var builder = new ExtractableCohortAuditLogBuilder();

            var cic = WhenIHaveA<CohortIdentificationConfiguration>();
            var desc = builder.GetDescription(cic);

            var moqCohort = Mock.Of<IExtractableCohort>(e => e.AuditLog == desc);
            var cic2 = builder.GetObjectIfAny(moqCohort, RepositoryLocator);

            Assert.IsNotNull(cic2);
            Assert.IsInstanceOf<CohortIdentificationConfiguration>(cic2);
            Assert.AreEqual(cic,cic2);
        }

        [Test]
        public void AuditLogReFetch_ExtractionInformation()
        {
            var builder = new ExtractableCohortAuditLogBuilder();

            var ei = WhenIHaveA<ExtractionInformation>();
            var desc = builder.GetDescription(ei);

            var moqCohort = Mock.Of<IExtractableCohort>(e => e.AuditLog == desc);
            var ei2 = builder.GetObjectIfAny(moqCohort, RepositoryLocator);

            Assert.IsNotNull(ei2);
            Assert.IsInstanceOf<ExtractionInformation>(ei2);
            Assert.AreEqual(ei, ei2);
        }

        [Test]
        public void AuditLogReFetch_WhenAuditLogIsNull()
        {
            var builder = new ExtractableCohortAuditLogBuilder();
            var moqCohort = Mock.Of<IExtractableCohort>(e => e.AuditLog == null);
            Assert.IsNull(builder.GetObjectIfAny(moqCohort, RepositoryLocator));
        }
        [Test]
        public void AuditLogReFetch_WhenAuditLogIsRubbish()
        {
            var builder = new ExtractableCohortAuditLogBuilder();
            var moqCohort = Mock.Of<IExtractableCohort>(e => e.AuditLog == "troll doll dur I invented this cohort myself");
            Assert.IsNull(builder.GetObjectIfAny(moqCohort, RepositoryLocator));
        }

        [Test]
        public void AuditLogReFetch_WhenSourceIsDeleted()
        {
            var builder = new ExtractableCohortAuditLogBuilder();

            var ei = WhenIHaveA<ExtractionInformation>();
            var desc = builder.GetDescription(ei);

            var moqCohort = Mock.Of<IExtractableCohort>(e => e.AuditLog == desc);
            
            // delete the source
            ei.DeleteInDatabase();
            
            // should now return null
            Assert.IsNull(builder.GetObjectIfAny(moqCohort, RepositoryLocator));
        }
    }
}
