## SampleTwitter
A sample twitter app(pure CRUD), intended to be a practice project. Backend written in c#/.net core using simple
`MVC` pattern with `EF Core` as `ORM`, and client-side scripts written fully in Vue.js/typescript. 
Not caring about front-end part, 100% `slopping`. 

## Architecture
* `MVC` - easy to start with. Planning to rewrite using `VSA`.
* `EF Core` as `ORM` with `PostgreSQL` as db provider.
* `Serilog` - logging. Uses both `Console` and `Seq` as sinks
* `Cookie-based auth` - reasonable to use with browser as a client.
* `TestContainers` + `WebApplicationFactory` - imitate real world scenario during testing.
* `OpenApi` + `Scalar` - UI is just so beautiful.

## What is planned but not implemented yet?
1) Want to implement `SSO` scenarios using `Google` provider. 
2) Use `Aspire` for integration tests.

... And a bunch of other stuff.

## License
The project is distributed under the terms of the [MIT license](https://github.com/blendereru/sample-twitter/blob/main/LICENSE).