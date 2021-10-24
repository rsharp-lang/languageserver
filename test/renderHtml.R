const model = engine::parse(`${@dir}/demo.R`);

writeLines(toHtml(model), con = `${@dir}/demo.html`);