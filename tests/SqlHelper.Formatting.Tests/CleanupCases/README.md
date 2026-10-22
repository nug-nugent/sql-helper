Spec cases for the clean-up command. Each `name.sql` becomes `name.expected.sql`. Only the tokens the clean-up rules
rewrite change (join keywords, abbreviated keywords, `!=`, aliases, brackets); layout and everything else stay as typed.
Names bracketed because they are keywords are listed in `src/SqlHelper.Formatting/Rewrites/BracketedNames.txt`.