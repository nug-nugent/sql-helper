merge dbo.T as t using dbo.S as s on t.Id = s.Id when matched then update set t.A = s.A when not matched then insert (Id, A) values (s.Id, s.A);
