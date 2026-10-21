select Id, row_number() over (partition by Grp order by Dt desc) as Rn, sum(Amt) over (partition by Grp) as Tot from dbo.T order by Id
