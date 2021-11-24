--Version:7.0.0
--Description: Increases the width of fields in Catalogue table

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Detail_Page_URL')
	ALTER TABLE Catalogue ALTER COLUMN Detail_Page_URL nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Type')
	ALTER TABLE Catalogue ALTER COLUMN [Type] nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Geographical_coverage')
	ALTER TABLE Catalogue ALTER COLUMN Geographical_coverage nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Time_coverage')
	ALTER TABLE Catalogue ALTER COLUMN Time_coverage nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Update_sched')
	ALTER TABLE Catalogue ALTER COLUMN Update_sched nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Contact_details')
	ALTER TABLE Catalogue ALTER COLUMN Contact_details nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Resource_owner')
	ALTER TABLE Catalogue ALTER COLUMN Resource_owner nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Attribution_citation')
	ALTER TABLE Catalogue ALTER COLUMN Attribution_citation nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Access_options')
	ALTER TABLE Catalogue ALTER COLUMN Access_options nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='API_access_URL')
	ALTER TABLE Catalogue ALTER COLUMN API_access_URL nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Browse_URL')
	ALTER TABLE Catalogue ALTER COLUMN Browse_URL nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Bulk_Download_URL')
	ALTER TABLE Catalogue ALTER COLUMN Bulk_Download_URL nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Query_tool_URL')
	ALTER TABLE Catalogue ALTER COLUMN Query_tool_URL nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Source_URL')
	ALTER TABLE Catalogue ALTER COLUMN Source_URL nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Granularity')
	ALTER TABLE Catalogue ALTER COLUMN Granularity nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Country_of_origin')
	ALTER TABLE Catalogue ALTER COLUMN Country_of_origin nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Data_standards')
	ALTER TABLE Catalogue ALTER COLUMN Data_standards nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Administrative_contact_name')
	ALTER TABLE Catalogue ALTER COLUMN Administrative_contact_name nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Administrative_contact_email')
	ALTER TABLE Catalogue ALTER COLUMN Administrative_contact_email nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Administrative_contact_telephone')
	ALTER TABLE Catalogue ALTER COLUMN Administrative_contact_telephone nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Administrative_contact_address')
	ALTER TABLE Catalogue ALTER COLUMN Administrative_contact_address nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='Source_of_data_collection')
	ALTER TABLE Catalogue ALTER COLUMN Source_of_data_collection nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalogue' AND COLUMN_NAME='SubjectNumbers')
	ALTER TABLE Catalogue ALTER COLUMN SubjectNumbers nvarchar(max) 



IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CatalogueItem' AND COLUMN_NAME='Topic')
	ALTER TABLE CatalogueItem ALTER COLUMN Topic nvarchar(max) 
	
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CatalogueItem' AND COLUMN_NAME='Periodicity')
	ALTER TABLE CatalogueItem ALTER COLUMN Periodicity nvarchar(max) 

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CatalogueItem' AND COLUMN_NAME='Agg_method')
	ALTER TABLE CatalogueItem ALTER COLUMN Agg_method nvarchar(max) 
