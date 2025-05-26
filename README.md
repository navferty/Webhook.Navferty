# Webhook.Navferty

Simple web application for testing web requests.

## Overview

Any HTTP request received by the server will be saved in the database and displayed at [webhook.navferty.com](https://webhook.navferty.com/).
Base URL for requests includes unique identifier for each user, so that requests are stored separately.

This tool is useful for testing webhooks and other integrations. If you need to test your application,
which sends HTTP requests to external services, or if you want to capture requests and responses
for debugging purposes, you can use this tool.

The application allows you to add pre-configured responses for specific URLs.

## Usage

When you initially open the web application, you will be redirected with `tenantId` query parameter.
It contains the unique identifier of your tenant, which is used to store your requests and responses.

To perform a request, you need to use the base URL in the format:
```
https://webhook.navferty.com/{tenantId}/{any-path}?{query-parameters}
```

For example, if your `tenantId` is `12345`, you can perform a request to:
```
https://webhook.navferty.com/12345/test?param1=value1&param2=value2
```

All recorded requests will be displayed at main page of the application.
You can click on the request to see its details, including all headers, body, and query parameters.

## Configure Responses

You can configure responses for specific URLs by going to the [ConfigureResponses](https://webhook.navferty.com/ConfigureResponses) page.

You can add a new response by specifying the URL pattern and the response body.

For example, if you want to return a JSON response for requests to `https://webhook.navferty.com/12345/test`, you can add a response with the URL pattern `/test` and the response body as JSON.

Supported response formats include plain text, JSON, and HTML.

## Self-hosted option

You can run your own instance of application using Docker compose. Just download the
[docker-compose.yml](https://raw.githubusercontent.com/navferty/webhook.navferty/master/docker-compose.yml)
file and run `docker compose up -d` command in the directory with this file.

The application will be available at `http://localhost:8088` and the database
will be stored in the `db_data` Docker volume.
