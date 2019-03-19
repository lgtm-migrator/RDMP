--Version:2.13.0.1
--Description: Adds a column called ID to JoinInfo allowing it to be created/deleted more easily.

if( exists (select * from sys.key_constraints where type ='PK' AND OBJECT_NAME(parent_object_id) = 'JoinInfo'))
	ALTER TABLE JoinInfo DROP CONSTRAINT PK_JoinInfo;  

  if(not exists (select * from sys.all_columns where name ='ID' AND OBJECT_NAME(object_id) = 'JoinInfo'))
  	ALTER TABLE JoinInfo ADD ID INT IDENTITY(1,1)

  if(not exists (select * from sys.key_constraints where type ='PK' AND OBJECT_NAME(parent_object_id) = 'JoinInfo'))
	ALTER TABLE JoinInfo ADD CONSTRAINT PK_JoinInfo PRIMARY KEY (ID);

  if not exists (select 1 from sys.indexes where name = 'ix_JoinColumnsMustBeUnique')
	CREATE UNIQUE NONCLUSTERED INDEX [ix_JoinColumnsMustBeUnique] ON [JoinInfo]
	(
		[ForeignKey_ID] ASC,
		[PrimaryKey_ID] ASC
	)


