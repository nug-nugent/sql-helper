begin try
    select 1
end try
begin catch
    if @@trancount > 0 rollback tran
    select error_message()
end catch
select 2
