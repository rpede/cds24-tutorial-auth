# Introduction

## Quick-start

The quickest way to start the app is with:

```sh
docker compose -f fullstack.yml up --build
```

It will run everything in containers including database, client and server.

You can cleanup by running:

```sh
docker compose -f fullstack.yml down --remove-orphans
```

## Development

For development, you should run both client and server directly from the
file-system.
Since it will automatically reload when you change the files.

**Setup**

```sh
docker compose  up
npm ci --prefix client
```

**Client**

```sh
npm run dev --prefix client
```

**Server**

```sh
dotnet watch --project server/Api
```

You can also run the applications from your IDE instead if you want a debugger
attached.

You can reset the database with:

```sh
docker compose down --remove-orphans && docker compose up 
```

## How to use

The application is a blog-like application with markdown support.

Authentication, authorization, user and session management is something you
will implement through a small series of exercises.

All development settings are defined in
[appsettings.Development.json](server/Api/appsettings.Development.json).
Settings for deployment can be specified in `appsettings.Production.json`.
All secrets for deployment should be set using [Google Cloud - Secret
Manager](https://cloud.google.com/secret-manager/docs/).

Here are the URLs to access the various parts.

When running directly from file-system:

| Sub-system  | URL                                            |
| ----------- |------------------------------------------------|
| Client      | <http://localhost:5173/>                       |
| Server      | <http://localhost:5088/api/swagger/index.html> |
| MailCatcher | <http://localhost:1080/>                       |

When running the full stack with docker:

| Sub-system  | URL                      |
| ----------- | ------------------------ |
| Client      | <http://localhost:1080/> |
| MailCatcher | <http://localhost:1080/> |

**client** will proxy request with base-path `/api` to **backend**.

Database:

| Parameter         | Value                                                           |
| ----------------- | --------------------------------------------------------------- |
| URL               | jdbc:postgresql://localhost:5432/postgres                       |
| Username          | postgres                                                        |
| Password          | mysecret                                                        |
| Connection string | HOST=localhost;DB=postgres;UID=postgres;PWD=mysecret;PORT=5432; |

## Users

The application ships with some test data.
That includes users with different roles.
After implementing authentication, you will be able to log-in using these credentials:

| Email / Username        | Password | Role   |
|-------------------------| -------- | ------ |
| admin@example.com       | S3cret!  | Admin  |
| editor@example.com      | S3cret!  | Editor |
| othereditor@example.com | S3cret!  | Editor |
| reader@example.com      | S3cret!  | Reader |

## Tech-stack

The example application mostly based on technology you are already familiar
with.

### Server

- [ASP.NET](https://dotnet.microsoft.com/en-us/apps/aspnet) with [Controller-based APIs](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0) - framework
- [PostgreSQL](https://www.postgresql.org/) - database
- [Docker](https://www.docker.com/) - containers
- [FluentValidation](https://docs.fluentvalidation.net/en/latest/) - validation
- [Swagger (Swashbuckle)](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) - REST specification & UI

### Client

- [React](https://react.dev/) - framework
- [tailwindcss](https://tailwindcss.com/) - styling
- [daisyUI](https://daisyui.com/) - component library
- [Axios](https://axios-http.com/) with [swagger-typescript-api](https://github.com/acacode/swagger-typescript-api) - HTTP/API client
- [Jotai](https://jotai.org/) - state
- [React Router](https://reactrouter.com/en/main)
- [React Hook Form](https://react-hook-form.com/) - form handling & data loading
- [yup](https://github.com/jquense/yup) - validation
- [react-markdown](https://github.com/remarkjs/react-markdown#readme) - markdown rendering

There are a couple of new libraries that you are (likely) not familiar with on
in the client.
They aren't that important for the exercises.
Just in case you are curious.
Here is a quick rundown.

**React Hook Form**

This is probably the most noticeable difference from what you are used to.
It provides a `useForm` hook that can take care of managing state + validation
for input elements and submitting forms.

**React Router loader**

React router is nothing new.
But, I'm using the [loader api](https://reactrouter.com/en/main/route/loader)
to fetch data from API on route navigation.

You are likely used to load data with `useEffect` hook like
[this](https://github.com/uldahlalex/live_api_react_danish_class/blob/master/client/src/App.tsx#L14-L23).
Loading data through the router simply means it doesn't have to be dealt with
within the component.

**yup**

Is a validation library similar to FluentValidation (but for React).

**react-markdown**

It renders text written using [Markdown](https://www.markdownguide.org/) syntax, so it
looks nice.
Markdown is a simple easy to learn markup language well suited for writing
programming documentation.
In fact that is how documentation on GitHub is written.
This document you are reading right now is written in Markdown.

## Workshop

Complete these exercises in order, since each builds on the previous.
Make a commit each time you complete an exercise.

0. [Authentication](./tutorial/00-authentication.md)
1. [Sessions](./tutorial/01-session.md)
1. [Authorization](./tutorial/02-authorization.md)
