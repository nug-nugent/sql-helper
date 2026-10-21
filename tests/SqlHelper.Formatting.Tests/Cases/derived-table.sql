select x.Id from (select Id, Name from dbo.T inner join dbo.U on U.Id = T.Id where Name like 'a%') x inner join dbo.Y y on y.Id = x.Id
