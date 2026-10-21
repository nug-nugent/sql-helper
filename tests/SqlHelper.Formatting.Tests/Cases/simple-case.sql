select case Status when 1 then 'Open' when 2 then 'Closed' else 'Unknown' end as StatusName from dbo.T
