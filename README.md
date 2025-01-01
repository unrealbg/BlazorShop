[![.NET](https://github.com/unrealbg/BlazorShop/actions/workflows/dotnet.yml/badge.svg)](https://github.com/unrealbg/BlazorShop/actions/workflows/dotnet.yml)

## Table of Contents
- [Introduction](#introduction)
- [Who Is It For?](#who-is-it-for)
- [Features](#features)
- [Technologies Used](#technologies-used)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Screenshots](#screenshots)
- [Contributing](#contributing)
- [Demo](#demo)
- [License](#license)
- [Acknowledgements](#acknowledgements)

## Introduction
BlazorShop is an open-source e-commerce platform designed to provide an efficient and user-friendly solution for managing online stores. Built with Blazor, it delivers a seamless experience for both administrators and customers.

### Who Is It For?
BlazorShop is ideal for:
- Small to medium-sized businesses looking to manage their online store efficiently.
- Developers wanting to explore Blazor WebAssembly and clean architecture in real-world projects.

## Features
- **Product Management**: Add, edit, and remove products effortlessly.
- **Category Management**: Organize products into categories for better navigation and management.
- **Order Management**: Handle customer orders with an intuitive interface.
- **Customer Management**: Track customer information and their order history.
- **Customization**: Adaptable to your specific business needs.

## Technologies Used
- **Backend**: ASP.NET Core Web API
- **Frontend**: Blazor WebAssembly
- **Database**: Microsoft SQL Server (MSSQL)
- **Object-Relational Mapping**: EntityFramework Core
- **Mapping**: AutoMapper
- **API Documentation**: Swashbuckle (Swagger)

## Requirements
Before getting started, ensure you have the following:
- .NET 9 SDK or later
- SQL Server instance (local or cloud)
- A web browser that supports WebAssembly (e.g., Chrome, Edge, Firefox)

## Getting Started
Follow these steps to set up and run BlazorShop:

1. **Clone the repository**:
    ```sh
    git clone https://github.com/unrealbg/BlazorShop.git
    ```
2. **Navigate to the project directory**:
    ```sh
    cd BlazorShop
    ```
3. **Set up the database**:
    - Update the `appsettings.json` file with your SQL Server connection string.
    - Apply migrations (if applicable):
      ```sh
      dotnet ef database update
      ```
4. **Build and run the project**:
    ```sh
    dotnet run
    ```
## Screenshots

### User Home View
![User Home View](https://vps.unrealbg.com/blazorblog/UserHomeView.png)

### Register Page
![Register Page](https://vps.unrealbg.com/blazorblog/RegisterPage.png)

### Login Page
![Login Page](https://vps.unrealbg.com/blazorblog/LoginPage.png)

### User Dropdown Menu
![User Dropdown Menu](https://vps.unrealbg.com/blazorblog/UserDropDown.png)

### My Profile Page
![My Profile Page](https://vps.unrealbg.com/blazorblog/MyProfilePage.png)

### Change Password
![Change Password](https://vps.unrealbg.com/blazorblog/ChangePassword.png)

### User Adds Product to Cart Once
![User Adds Product to Cart Once](https://vps.unrealbg.com/blazorblog/UserAddProductToCardOnce.png)

### User Adds Same Product Twice
![User Adds Same Product Twice](https://vps.unrealbg.com/blazorblog/UserAddSameProductTwice.png)

### See More Product Details
![See More Product Details](https://vps.unrealbg.com/blazorblog/SeeMore.png)

### User Cart
![User Cart](https://vps.unrealbg.com/blazorblog/UserCart.png)

### User Select Payment Method
![User Select Payment Method](https://vps.unrealbg.com/blazorblog/UserSelectPaymentMethod.png)

### Payment Process
![Payment Process](https://vps.unrealbg.com/blazorblog/Paying.png)

### Payment Success
![Payment Success](https://vps.unrealbg.com/blazorblog/PaymentSuccess.png)

### Add Category
![Add Category](https://vps.unrealbg.com/blazorblog/AddCategory.png)

### Add Category Confirmation
![Add Category Confirmation](https://vps.unrealbg.com/blazorblog/AddCategory2.png)

### Product Management Page
![Product Management Page](https://vps.unrealbg.com/blazorblog/AddProductPage.png)

### Add Product to Category
![Add Product to Category](https://vps.unrealbg.com/blazorblog/AddProductToCategory.png)

### Product Added Successfully
![Product Added Successfully](https://vps.unrealbg.com/blazorblog/ProductAddedSusscessfully.png)

### Delete Category Confirmation
![Delete Category Confirmation](https://vps.unrealbg.com/blazorblog/DeleteCategoty.png)

### Category Deletion Success
![Category Deletion Success](https://vps.unrealbg.com/blazorblog/CategoryDeleteSuccessfully.png)

### Sales Page
![Sales Page](https://vps.unrealbg.com/blazorblog/SalesPage.png)

## Contributing
We welcome contributions to BlazorShop! Here’s how you can get involved:
1. Fork the repository.
2. Create a new branch:
    ```sh
    git checkout -b feature-branch
    ```
3. Make your changes and commit them:
    ```sh
    git commit -m 'Add new feature'
    ```
4. Push to your branch:
    ```sh
    git push origin feature-branch
    ```
5. Open a pull request.

## Demo
Check out a live demo of BlazorShop [shop.unrealbg.com](https://shop.unrealbg.com).

## License
BlazorShop is licensed under the MIT License. See the [LICENSE](./LICENSE) file for more details.

## Acknowledgements
- **[unrealbg](https://github.com/unrealbg)**: The creator and main contributor of BlazorShop.
