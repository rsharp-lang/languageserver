const notebook as string = ?"--notebook" || stop("no notebook source file is specified!");
const htmlfile as string = ?"--output" || `${dirname(notebook)}/${basename(notebook)}.html`;

require(Rnotebook);

engine::parse(notebook)
|> pipHtml
|> writeLines(con = htmlfile)
;