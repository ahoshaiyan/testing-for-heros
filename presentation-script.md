# Test for Heros

## Integration Tests > Unit Tests

You should always write integration tests for your business code and avoid writing unit tests. Unit tests are suitable for small reusable blocks and classes that can function independentaliy and require non to minimum external services to do so.

Writing unit tests for your business code can lead to a lot of mocking and code that is ensured and tested to be working in isolation but not with other components.

With integration tests, you are hitting two birds with one stone, you are ensuring every code path is working and your business logic works as it should simulating the real world environment.

## Small Test > Large Tests

When testing a large business feature with a lot of branches and a process that can span a long time range, it is better to create a test class for that feature and a small test that ensures every aspect of the business process behave correctly.


## Write Stories Not Tests

Your tests should read like stories and should test a full business process from start to end while having small tests each asserting a specific aspect of that business process.

Let us have an example to better explain this. With our FoodApp, we want to test the full cycle from ordering from a restaurant to successful delivery then we will branch to other cases where the order can be rejected or failed to deliver.

We should start from our **Create Order** API which accepts a list of items that we want to order from some restaurant. A good test should be something like this:

- Test: **Create Order** should return `401` for un-authenticated requests
- Test: **Create Order** should return `403` for banned users with `message: you are not allowed to use the service, please contact customer support.`
- Test: **Create Order** should return `400` to prevent un-verified accounts from placing orders with `message: your email address is not verified, please verify it before attempting to order`.
- Test: **Create Order** should return `422` validation should fail when `message_to_driver` exceeds `2000` chars (this is a very importnant business rule as the SMS provider limit the number of chars per message).
    - We can also create tests for other validation rules
    - Some might argue that testing validations is just testing the validation library but this is not true, you are ensuring your business logic is tested and to prevent against someone chaning validations in future.
    - Valdiations are one of the areas that Coverage Tests cannot catch.
- Test: An invalid restaurant ID should return `400` with `message: Invalid restaurant ID`
- Test: Trying to order from a closed restaurant should return `400` with `message: Restaurant is closed`
- Test: Trying to order from a busy restaurant should return `400` with `message: Restaurant is currently busy and cannot accept additional orders`
- Test: correct account state and input data should create the order
    - The following are tests that depend on the previous test (each can run independently and can invoke the main tests, so they run in parallel with no issue).
    - Test: successul order should create an **Order** record in the database
    - Test: Order should have the authenticated user ID attached to it
    - Test: Order should have the restaurant ID attached to it
    - Test: Order should have `message_to_driver` added to it
    - Test: Order should have status set to `waiting_for_restaurant`
    - Test: a notification must be sent to the restaurant through browser push notifications.
    - Test: restaurant can reject the order with a valid reason
        - Test: rejected order should have status set to `rejected`
        - Test: rejected order should have the same reason set by the restaurant
        - Test: a notification must be sent to the user informing them that the order has been rejected by the restaurant.
    - Test: restaurant can accept the order
        - Test: accepted order status should be set to `preparing`
        - Test: a notification must be sent to the user
        - Test: a driver search process must be dispatched.


## Test for Your Business Not Coverage

Setting your goal to acheieve 100% test coverage is not good, since having 100% coverage does not mean you have tested you business logic 100%

Let's take the following example

Business Requirements: Message to delivery driver must not exceed 2000 charactars.

You could write tests that acheieve 100% test coverage for your **Create Order** API endpoint but completely miss the 2000 charactar limit business requirement. Your number one goal should be to cover all business logic then aim to 100% coverage.


## Do Not Mock

Mocking can help a lot with making tests easy to run and fast, it also reduces calls to metered API services that can cost a lot of money when running tests often.

You should avoid mocking services as much as possible and instead test against services you are using:
- Database
- Cache
- S3-Compatible Storage

Since 2015 you can easily (in a fraction of a second) start all those services in Docker, complete you tests then remove them.

When testing against the actual services you are using, you are guarnteeing that you code will work at least in the staging environment and increasing the likelyhood that it will work in production especially when using
a database specific feature like GIS, JSONB GIN Indexes etc. You will make sure that your application uses these functionality in the intended way with no breaking changes introduced since they were last tested.

Mocking should be used for services that can never be replicated locally, e.g. SMS gateway, latest exchange rates etc.


## Use Factories

Adopting factories can help your tests become faster + reduce the changes that different tests can intefere with one another.

Unlike fixtures, where you seed the database with all the data needed to perform tests leading to bad test startup time, and an increased change of data conflicts, factories allow you to create staging data on demand at begining of the test, complete your test then allow you test framework to rollback the test transaction in an efficient and safe manner.

