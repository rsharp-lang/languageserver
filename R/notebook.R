
#' Render Html from the rscript data
#'
#' @param rscript the rscript object data or the filepath of
#'    a rscript notebook document or the text contents of the
#'    rscript notebook.
#'
#' @return the generated html document content string.
#'
const pipHtml = function(rscript) {
    let css = system.file("assets/style.css", package = "Rnotebook");

    if (typeof rscript is "string") {
        rscript <- engine::parse(rscript);
    }

    rscript
    |> engine::toHtml(style = readText(css))
    ;
}
