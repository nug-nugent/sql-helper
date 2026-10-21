MERGE
	dbo.T AS t
USING
	dbo.S AS s
	ON t.Id = s.Id
WHEN MATCHED THEN
	UPDATE
	SET
		t.A = s.A
WHEN NOT MATCHED THEN
	INSERT (
		Id
		, A
	)
	VALUES (
		s.Id
		, s.A
	);