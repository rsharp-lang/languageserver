# # Demo for R# notebook
#
# this is an example of write ``R#`` notebook document, just like write
# a regular R# script at here.
#
# ## markdown document block in R# notebook
#
# the syntax of the markdown document block in the R# notebook just like 
# write a normal markdown document and the document text is a regular block
# code comments in R script. Every markdown document block in a R# notebook
# should be end with a single ``;`` symbol.
#
# ## demo R code block in notebook
#
# just write a markdown code block at here, the code that show below
# will not run when generate R notebook html document view:
#
# ```r
# #region "code block name"
#
# # demo of R# code
# const [x, y] as integer = [5, 5];
#
# print([x, y]);
# # [2] 5 5
# 
# # end region
# ;
# ```
#
# End of a code block in the R# notebook document code, should be ends with a 
# single ``;`` symbol too. 
#
;

#region "code"

#' Demo of function docs
#' 
#' @return this function returns nothing
#' 
const hello_notebook as function() {
  # comment internal
  return "world";
}

# show results
print(hello_notebook());
print(runif(101) * 99);

#end region
;

### list dump test
#
# just test the ``str`` object structure inspecter function in R language is work or not at here:
;

#region "structrue dump test"

# test of output structure dump
# of a list
str(list(
  seq = 1:50 step 0.3,
  logical = TRUE
));

const df = data.frame(
  x     = runif(10),
  y     = runif(10),
  z     = (runif(10) + 5) ^ 2,
  str   = "A",
  logic = FALSE
);

# previews of the data table and then view of the structures:
print(head(df));
str(df);

#end region
;

### image test
#
# the image outputs of the ``R#`` code will be encoded as
# base64 string and then embbled as image tag in the generated
# html document code.
;

#region "image test"

# scatter plot test
plot(runif(20), runif(20), 
  background = "white", 
  grid.fill  = "white", 
  color      = "skyblue",
  point_size = 23,
  shape      = "Triangle" 
);
#end region
;
