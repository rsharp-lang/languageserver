// export R# package module type define for javascript/typescript language
//
//    imports "engine" from "languageserver";
//
// ref=languageserver.RscriptEngine@languageserver, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null

/**
 * R# language server toolkit
 * 
*/
declare namespace engine {
   /**
    * listen on the tcp port for run the lsp http service
    * 
    * 
     * @param port -
     * 
     * + default value Is ``321``.
     * @param vscode_clr 
     * + default value Is ``null``.
   */
   function listen(port?: object, vscode_clr?: string): ;
   /**
    * Parse the given rscript as notebook rendering model
    * 
    * 
     * @param handle -
   */
   function parse(handle: string): object;
   /**
    * 
    * 
     * @param nb -
     * @param style the css style text
     * 
     * + default value Is ``null``.
     * @param strict 
     * + default value Is ``false``.
     * @param env 
     * + default value Is ``null``.
   */
   function toHtml(nb: object, style?: string, strict?: boolean, env?: object): string;
}
