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
const hello_notebook as function() {
	# comment internal
	return "world";
}

print(hello_notebook());
print(runif(101) * 99);

#end region
;

### list dump test
;

#region "list structrue"
str(list(
	seq = 1:50 step 0.3,
	logical = TRUE
));
#end region
;