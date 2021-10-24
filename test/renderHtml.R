engine::parse(`${@dir}/demo.R`)
|> pipHtml
|> writeLines(con = `${@dir}/demo.html`)
;