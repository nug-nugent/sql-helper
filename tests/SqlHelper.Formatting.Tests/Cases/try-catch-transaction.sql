begin try begin tran update dbo.T set A = 1 where B = 2 commit tran end try begin catch rollback tran; throw; end catch
