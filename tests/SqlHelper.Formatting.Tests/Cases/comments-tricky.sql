/*
   Multi-line
   block comment
*/
select a
from dbo.T
where x = 1
  -- only active rows
  and y = 2
if @a = 1
begin -- start
    -- leading comment
    select 1
end
