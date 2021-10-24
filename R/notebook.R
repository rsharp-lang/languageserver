const pipHtml as function(rscript) {
    const css as string = system.file("assets/style.css", package = "Rnotebook");

    if (typeof rscript == "string") {
        rscript = engine::parse(rscript);
    }

    rscript 
    |> toHtml(style = css)
    ;
}