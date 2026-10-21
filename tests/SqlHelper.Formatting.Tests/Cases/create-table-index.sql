create table dbo.T (Id int not null identity(1,1) primary key, Name varchar(50) null, constraint FK_x foreign key (Id) references dbo.U (Id))
create index IX_T on dbo.T (Name) include (Id) where Name is not null
