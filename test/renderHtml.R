require(languageserver);

engine::parse(here("demo.R"))
|> pipHtml
|> writeLines(con = here("demo.html"))
;