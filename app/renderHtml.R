require(Rnotebook);

#' Rendering R# notebook script
#'
#' @author xieguigang <xie.guigang@gcmodeller.org>
#' @description A cli pipeline R# script for run rendering 
#'    of the R# notebook script into html help document.
#' 

[@info "The R# source script file its filepath."]
[@type "filepath"]
const notebook as string = ?"--notebook" || stop("no notebook source file is specified!");
[@info "The output file path of the html document file that rendered from the given R# source script file."]
[@type "filepath"]
const htmlfile as string = ?"--output" || `${dirname(notebook)}/${basename(notebook)}.html`;

engine::parse(notebook)
|> pipHtml
|> writeLines(con = htmlfile)
;