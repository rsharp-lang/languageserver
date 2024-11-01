// export R# package module type define for javascript/typescript language
//
//    imports "engine" from "languageserver";
//
// ref=languageserver.RscriptEngine@languageserver, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null

/**
*/
declare namespace engine {
   /**
     * @param port default value Is ``321``.
   */
   function listen(port?: object): ;
   /**
   */
   function parse(handle: string): object;
   /**
     * @param style default value Is ``null``.
     * @param strict default value Is ``false``.
     * @param env default value Is ``null``.
   */
   function toHtml(nb: object, style?: string, strict?: boolean, env?: object): string;
}
