// export R# package module type define for javascript/typescript language
//
//    imports "Rsession" from "languageserver";
//
// ref=languageserver.Rsession@languageserver, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null

/**
 * 
*/
declare namespace Rsession {
   /**
    * # Terminate an R Session
    *  
    *  The function ``quit`` or its alias ``q`` terminate the current R session.
    * 
    * 
     * @param save a character string indicating whether the environment (workspace) should be saved, 
     *  one of ``"no"``, ``"yes"``, ``"ask"`` or ``"default"``.
     * 
     * + default value Is ``'default'``.
     * @param status the (numerical) error status to be returned to the operating system, where relevant. 
     *  Conventionally 0 indicates successful completion.
     * 
     * + default value Is ``0``.
     * @param runLast should ``.Last()`` be executed?
     * 
     * + default value Is ``true``.
     * @param envir 
     * + default value Is ``null``.
   */
   function q(save?: string, status?: object, runLast?: boolean, envir?: object): ;
   /**
    * # Terminate an R Session
    *  
    *  The function ``quit`` or its alias ``q`` terminate the current R session.
    * 
    * 
     * @param save a character string indicating whether the environment (workspace) should be saved, 
     *  one of ``"no"``, ``"yes"``, ``"ask"`` or ``"default"``.
     * 
     * + default value Is ``'no'``.
     * @param status the (numerical) error status to be returned to the operating system, where relevant. 
     *  Conventionally 0 indicates successful completion.
     * 
     * + default value Is ``0``.
     * @param runLast should ``.Last()`` be executed?
     * 
     * + default value Is ``true``.
     * @param envir 
     * + default value Is ``null``.
   */
   function quit(save?: string, status?: object, runLast?: boolean, envir?: object): ;
}
