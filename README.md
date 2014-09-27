# Middle. It's a MicroORM of sorts.

If you're like me and you actually don't loathe writing SQL at all. But you _do_ loathe writing the repetetive
ADO.NET code required to work with that SQL, then Middle might be of interest to you.

Middle is an abstraction built on top of ADO.NET that allows you to run typed (or dynamic) SQL queries. Just give it a connection, a type to map to, and a query.

## Installation
* Drop the Middle.cs code file into your app
* You may also need to add the System.Configuration reference into your app

## Usage
 * Get a Database
 * Add the connection to your database in the app.config/web.config
 * Create an instance of Middle and pass it the name of your connection
 * Do query stuff


## Show me some examples!
In these examples I'm using the AdventureWorksLT2012 database which you can get from [here](http://msftdbprodsamples.codeplex.com/).

### Running Queries

In my `App.config` file I've added the connection string to the database
  
	<connectionStrings>
		<add name="AdventureWorks"
			 connectionString="Server=<your server instance>; Database=AdventureWorksLT2012; Integrated Security=true"
			 providerName="System.Data.SqlClient" />
	</connectionStrings>

I have also created a super simple `Customer` model to store our data

	public class Customer {
		public CustomerId { get; set; }
		public FirstName { get; set; }
		public LastName { get; set; }
	}

Then in my service, repository, unit of work, whatever, I have instantiated Middle and passed it the connection string

	var db = new Middle("AdventureWorks");

Using this new `db` object we can run queries against the AdventureWorksLT2012 database like so...

	db.ExecuteQuery<Customer>("SELECT CustomerId, FirstName, LastName FROM SalesLT.Customer");

This call will return an IEnumerable of type Customer. It's important that the columns returned by the Query match the properties on our model. If they don't then simply alias them in your query!

### Parameters

You can parametize your queries in a couple of different ways:

You can be a little verbose:

	var query = String.Format("SELECT * FROM SalesLT.Customer WHERE CustomerId = {0}", 7);
	var customer = db.ExecuteQuerySingle<Customer>(query);

_ExecuteQuerySingle_ always returns one result

or alternatively you can pass parameters as numbered arguments directly:

	var customer = db.ExecuteQuerySingle<Customer>("SELECT * FROM SalesLT.Customer WHERE CustomerId = 0", 7)

## Stored Procedures
Middle can also run stored procedures by name, for example:

	var customers = db.ExecuteQuery<Customer>("sp_GetCustomers");

So long as that stored procedure exists it will be called and the results mapped.

You can also pass parameters to your stored procedure in the same way as before:

 	var customer = db.ExecuteQuery<Customer>("sp_GetCustomerById @0", 7);

 or

 	var query = String.Format("sp_GetCustomerById @id={0}", 7);
 	var customer = db.ExecuteQuery<Customer>(query);

