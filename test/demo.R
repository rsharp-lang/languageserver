# # H1 title
#
# this is an example of write ``R#`` notebook document, just like write
# a regular R# script at here.
#
# ## demo R code block in notebook
#
# just write a markdown code block at here, the code that show below
# will not run when generate R notebook html document view:
#
# ```r
# # demo of R# code
# const [x, y] as integer = [5, 5];
#
# print([x, y]);
# # [2] 5 5
# ```
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
  color      = "green"
);
#end region
;
