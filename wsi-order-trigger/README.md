# Overview
The `wsi-order-trigger` endpoint facilitates the insertion of a WSI order
into the WSI database. Additionally, it was SFTP any uploaded orders to WSI
as well.

# Submitting an Order to be Uploaded
Currently, there are two supported ways of uploading an order:
1. File upload
2. Bytes upload

## File Upload
File upload allows you to submit a `CSV` file containing orders and have them
uploaded.

### Attaching a File for Upload
A file can uploaded by appending a file object to the form data body of an
HTTP request with the key being `file`.

## String upload
In many cases, such as uploading a single order or when communicating between
different platforms, it may be easier to upload orders via a `string`.

### Submitting an Order in String Format
The body of a string upload must be a `CSV` string containing all of the data
for an order.

This will look something like this:
```
PTH,I,C123456780,123456780,C,07/30/2021,,,,75,,,"Harmeet Singh","13405 SE 30th St Suite 1A","Bellevue",WA,US,98005,,"Kevin Pugh","13405 SE 30th St Suite 1A","Bellevue",WA,US,98005,,,,,,,,FDXH,,,PGD,,HN,PGD,PP,,,,,,Y,,,,PT,,,,,,,,,,,,
PTD,I,C123456780,1,A,100470,,,,,1,1,,,123.45,,,HN,PGD,,,,,,,,
```
Ensure that PTH and PTD are on separate lines with all their respective
information on the same line.

**Although not necessary, it is good habit to submit addresses in quotes to
avoid errors when WSI processes the orders.**
