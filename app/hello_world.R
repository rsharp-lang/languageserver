# # Demo test for markdown
# This is a paragraph in the markdown document.
# ## H2 title
# This is another paragraph in the markdown document.
# ### DEMO1: Markdown list
# Here is a list of supported features in R# notebook:
#
# + markdown document
# + code block
# + image output of the code block
#
# ### DEMO2: Table test
# Create a table in document just like what you do in the markdown document:
#
# |Features                      |supported|
# |------------------------------|---------|
# |markdown document             |yes      |
# |code block                    |yes      |
# |image output of the code block|yes      |
#
# ### DEMO3: Code test
# Yes, you can write a code block in markdown document block:
# ```r
# print("Hello world!");
# ```
# > But this code block will not be evaluated
;

# # R# code block demo 
# One code block in the R# notebook should be start with a 
# tag: ``region "name"``, and then should be end with a tag:
# ``end region``. And also the code block should be end with
# a individual semicolon symbol.
#
# syntax example as:
#
# ```r
# #region "block name"
# ...
# #end region
# ;
# ```
;

#region "Rscript code demo"
const word as string = ["World", "R#", "User"];

# print string contents at here will also be
# rendered on the html document!
print(`Hello ${word}!`);
#end region
;