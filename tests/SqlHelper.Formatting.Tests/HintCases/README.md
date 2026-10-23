Spec cases for Show Performance Hints. Checking each `name.sql` must give exactly the hints in `name.expected.txt`,
one per line as `sqlhelper hints` prints them after the file name (`line:column: warning|suggestion SHnnn: message`),
in script order. An empty expected file means no hints. Every code is listed in `docs/style-guide.md`, Performance Hints.