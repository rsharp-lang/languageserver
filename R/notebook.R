
#' Render Html from the rscript data
#'
#' @param rscript the rscript object data or the filepath of
#'    a rscript notebook document or the text contents of the
#'    rscript notebook.
#'
#' @return the generated html document content string.
#'
const pipHtml as function(rscript) {
    const css as string = system.file("assets/style.css", package = "Rnotebook");

    if (typeof rscript is "string") {
        rscript = engine::parse(rscript);
    }

    rscript
    |> toHtml(style = readText(css))
    ;
}
