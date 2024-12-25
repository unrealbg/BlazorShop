/*---------------------------------------------------------------------
    File Name: custom.js
---------------------------------------------------------------------*/

(function () {
    "use strict";

    // Preloader
    setTimeout(function () {
        $('.loader_bg').fadeToggle();
    }, 1500);

    $(document).ready(function () {
        // Initialize meanmenu
        $('header nav').meanmenu();

        // Initialize tooltip
        $('[data-toggle="tooltip"]').tooltip();

        // Sticky header
        $(".sticky-wrapper-header").sticky({ topSpacing: 0 });

        // Mouseover overlay for megamenu
        $(".main-menu ul li.megamenu").on({
            mouseover: function () {
                if (!$(this).parent().hasClass("#wrapper")) {
                    $("#wrapper").addClass('overlay');
                }
            },
            mouseleave: function () {
                $("#wrapper").removeClass('overlay');
            }
        });

        // Banner rotator slider
        var owl = $('.banner-rotator-slider');
        if (owl.length) {
            owl.owlCarousel({
                items: 1,
                loop: true,
                margin: 10,
                nav: true,
                dots: false,
                navText: ["<i class='fa fa-angle-left'></i>", "<i class='fa fa-angle-right'></i>"],
                autoplay: true,
                autoplayTimeout: 3000,
                autoplayHoverPause: true
            });
        }

        // Back to top button
        $(window).on('scroll', function () {
            let scroll = $(window).scrollTop();
            if (scroll >= 100) {
                $("#back-to-top").addClass('b-show_scrollBut');
            } else {
                $("#back-to-top").removeClass('b-show_scrollBut');
            }
        });

        $("#back-to-top").on("click", function () {
            $('body,html').animate({
                scrollTop: 0
            }, 1000);
        });

        // Contact form validation
        $.validator.setDefaults({
            submitHandler: function () {
                alert("Form submitted!");
            }
        });

        $("#contact-form").validate({
            rules: {
                firstname: "required",
                email: {
                    required: true,
                    email: true
                },
                lastname: "required",
                message: "required",
                agree: "required"
            },
            messages: {
                firstname: "Please enter your firstname",
                email: "Please enter a valid email address",
                lastname: "Please enter your lastname",
                message: "Please enter your Message",
                agree: "Please accept our policy"
            },
            errorElement: "div",
            errorPlacement: function (error, element) {
                error.addClass("help-block");
                if (element.prop("type") === "checkbox") {
                    error.insertAfter(element.parent("input"));
                } else {
                    error.insertAfter(element);
                }
            },
            highlight: function (element) {
                $(element).parents(".col-md-4, .col-md-12").addClass("has-error").removeClass("has-success");
            },
            unhighlight: function (element) {
                $(element).parents(".col-md-4, .col-md-12").addClass("has-success").removeClass("has-error");
            }
        });

        // Swiper slider initialization
        if ($('.heroslider').length) {
            new Swiper('.heroslider', {
                spaceBetween: 30,
                centeredSlides: true,
                slidesPerView: 'auto',
                loop: true,
                autoplay: {
                    delay: 2500,
                    disableOnInteraction: false,
                },
                pagination: {
                    el: '.swiper-pagination',
                    clickable: true,
                    dynamicBullets: true
                }
            });
        }

        // Product Filters Swiper
        if ($('.swiper-product-filters').length) {
            new Swiper('.swiper-product-filters', {
                slidesPerView: 3,
                slidesPerColumn: 2,
                spaceBetween: 30,
                breakpoints: {
                    1024: {
                        slidesPerView: 3,
                        spaceBetween: 30,
                    },
                    768: {
                        slidesPerView: 2,
                        spaceBetween: 30,
                        slidesPerColumn: 1,
                    },
                    640: {
                        slidesPerView: 2,
                        spaceBetween: 20,
                        slidesPerColumn: 1,
                    },
                    480: {
                        slidesPerView: 1,
                        spaceBetween: 10,
                        slidesPerColumn: 1,
                    }
                },
                pagination: {
                    el: '.swiper-pagination',
                    clickable: true,
                    dynamicBullets: true
                }
            });
        }

        // Countdown timer
        $('[data-countdown]').each(function () {
            var $this = $(this),
                finalDate = $(this).data('countdown');

            $this.countdown(finalDate, function (event) {
                $(this).html(event.strftime(''
                    + '<div class="time-bar"><span class="time-box">%w</span> <span class="line-b">weeks</span></div> '
                    + '<div class="time-bar"><span class="time-box">%d</span> <span class="line-b">days</span></div> '
                    + '<div class="time-bar"><span class="time-box">%H</span> <span class="line-b">hr</span></div> '
                    + '<div class="time-bar"><span class="time-box">%M</span> <span class="line-b">min</span></div> '
                    + '<div class="time-bar"><span class="time-box">%S</span> <span class="line-b">sec</span></div>'));
            });
        });

        // Sidebar toggle
        $('#sidebarCollapse').on('click', function () {
            $('#sidebar').toggleClass('active');
            $(this).toggleClass('active');
        });

        // Blog carousel
        $('#blogCarousel').carousel({
            interval: 5000
        });

    });
})();
