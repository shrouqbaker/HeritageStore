using Microsoft.AspNetCore.Identity;
using HeritageStore.Models;

namespace HeritageStore.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. الـRoles
            string[] roles = { "Admin", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // 2. حساب Admin
            string adminEmail = "admin@heritagestore.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    Status = "active",
                    CreatedAt = DateTime.Now
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // 3. حساب بائع تجريبي (لعرض منتجات واقعية)
            string sellerEmail = "layla.seller@example.com";
            var sellerUser = await userManager.FindByEmailAsync(sellerEmail);
            if (sellerUser == null)
            {
                sellerUser = new ApplicationUser
                {
                    UserName = sellerEmail,
                    Email = sellerEmail,
                    EmailConfirmed = true,
                    Bio = "حرفية متخصصة بتطريز الأثواب الفلسطينية التقليدية منذ أكثر من 15 عامًا، أعمل على إحياء النقشات القديمة بخيوط طبيعية.",
                    ProfileImage = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=300",
                    Status = "active",
                    CreatedAt = DateTime.Now
                };
                var result = await userManager.CreateAsync(sellerUser, "Seller@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(sellerUser, "User");
            }

            // 4. التصنيفات (بصور حقيقية من Unsplash)
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { Name = "الأثواب الفلسطينية", ImageUrl = "https://images.unsplash.com/photo-1622470953794-aa9c70b0fb9d?w=800&auto=format&fit=crop" },
                    new Category { Name = "الأثواب الأردنية", ImageUrl = "https://images.unsplash.com/photo-1594736797933-d0501ba2fe65?w=800&auto=format&fit=crop" },
                    new Category { Name = "التطريز والأدوات", ImageUrl = "https://images.unsplash.com/photo-1610030469983-98e550d6193c?w=800&auto=format&fit=crop" },
                    new Category { Name = "القطع التراثية المكملة", ImageUrl = "https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=800&auto=format&fit=crop" }
                );
                await context.SaveChangesAsync();
            }

            // 5. أنواع التطريز
            if (!context.EmbroideryTypes.Any())
            {
                context.EmbroideryTypes.AddRange(
                    new EmbroideryType { Name = "نابلسي", Country = "فلسطين" },
                    new EmbroideryType { Name = "خليلوي", Country = "فلسطين" },
                    new EmbroideryType { Name = "غزاوي", Country = "فلسطين" },
                    new EmbroideryType { Name = "بيت لحمي", Country = "فلسطين" },
                    new EmbroideryType { Name = "يافاوي", Country = "فلسطين" },
                    new EmbroideryType { Name = "عماني", Country = "الأردن" },
                    new EmbroideryType { Name = "سلطي", Country = "الأردن" },
                    new EmbroideryType { Name = "معاني", Country = "الأردن" },
                    new EmbroideryType { Name = "كركي", Country = "الأردن" }
                );
                await context.SaveChangesAsync();
            }

            // 6. منتجات تجريبية واقعية
            if (!context.Products.Any())
            {
                var palestineCategory = context.Categories.First(c => c.Name == "الأثواب الفلسطينية");
                var jordanCategory = context.Categories.First(c => c.Name == "الأثواب الأردنية");
                var yafawi = context.EmbroideryTypes.First(e => e.Name == "يافاوي");
                var khalili = context.EmbroideryTypes.First(e => e.Name == "خليلوي");
                var karaki = context.EmbroideryTypes.First(e => e.Name == "كركي");

                var product1 = new Product
                {
                    UserId = sellerUser.Id,
                    CategoryId = palestineCategory.CategoryId,
                    EmbroideryTypeId = yafawi.EmbroideryTypeId,
                    Country = "فلسطين",
                    Name = "ثوب يافا الملكي",
                    Description = "قطعة محفوظة نادرة تمثل براعة التطريز الفلسطيني الأصيل، صُنع هذا الثوب بعناية فائقة على قماش الكتان الطبيعي.",
                    Story = "ورثت \"لبى بافا\" لحظة تجسد روح المدينة الساحلية الفياضة بالحياة، رُسمت خيوطه بألوان تروي حكاية بيارات يافا وأشجار الحمضيات المتوسطية.",
                    HistoricalBackground = "يعود تصميم هذا الثوب إلى أوائل القرن العشرين، وهي فترة شهدت ازدهارًا اقتصاديًا وثقافيًا ملحوظًا. كانت المدينة مركزًا تجاريًا رئيسيًا، مما انعكس على تطريز الثوب المستخدمة في تلك الحقبة.",
                    Occasion = "المناسبات الخاصة وحفلات الزفاف",
                    PickupAddress = "يافا، الساحل الفلسطيني",
                    ListingType = "for_sale",
                    Price = 2450,
                    Status = "available",
                    CreatedAt = DateTime.Now.AddDays(-5)
                };

                var product2 = new Product
                {
                    UserId = sellerUser.Id,
                    CategoryId = palestineCategory.CategoryId,
                    EmbroideryTypeId = khalili.EmbroideryTypeId,
                    Country = "فلسطين",
                    Name = "ثوب الخليل التراثي",
                    Description = "ثوب مطرز بنقشات خليلية دقيقة، يجمع بين الألوان الترابية الدافئة وتفاصيل هندسية متقنة.",
                    Story = "قطعة توارثتها الأجيال في عائلة خليلية، وتحمل نقشة \"شجرة السرو\" الرمز الأبرز لصمود المنطقة وثباتها.",
                    HistoricalBackground = "التطريز الخليلي يتميز بغرزه الكثيفة وألوانه الغامقة، ويعود إلى تقاليد الزراعة والكروم المنتشرة في جبال الخليل.",
                    Occasion = "المناسبات اليومية والاجتماعية",
                    PickupAddress = "الخليل، فلسطين",
                    ListingType = "for_sale",
                    Price = 1800,
                    Status = "available",
                    CreatedAt = DateTime.Now.AddDays(-2)
                };

                var product3 = new Product
                {
                    UserId = sellerUser.Id,
                    CategoryId = jordanCategory.CategoryId,
                    EmbroideryTypeId = karaki.EmbroideryTypeId,
                    Country = "الأردن",
                    Name = "ثوب الكرك الأصيل",
                    Description = "ثوب أردني تقليدي من منطقة الكرك، بتطريز مميز بالخيوط الحمراء والسوداء على قماش أسود كثيف.",
                    Story = "يعكس هذا الثوب هوية نساء الكرك القويّة، وارتبط تاريخيًا بمناسبات الأعراس والاحتفالات الكبرى في المنطقة.",
                    HistoricalBackground = "تشتهر الكرك بتطريزها المائل للسواد والأحمر الغامق، وهو أسلوب متوارث منذ عقود يعكس طبيعة الحياة في الجنوب الأردني.",
                    Occasion = "حفلات الزفاف",
                    PickupAddress = "الكرك، الأردن",
                    ListingType = "archive_only",
                    Status = "approved",
                    CreatedAt = DateTime.Now.AddDays(-10)
                };

                context.Products.AddRange(product1, product2, product3);
                await context.SaveChangesAsync();

                // صور المنتجات
                context.ProductImages.AddRange(
                    new ProductImage { ProductId = product1.ProductId, ImageUrl = "https://images.unsplash.com/photo-1622470953794-aa9c70b0fb9d?w=800&auto=format&fit=crop", IsMain = true },
                    new ProductImage { ProductId = product2.ProductId, ImageUrl = "https://images.unsplash.com/photo-1594736797933-d0501ba2fe65?w=800&auto=format&fit=crop", IsMain = true },
                    new ProductImage { ProductId = product3.ProductId, ImageUrl = "https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=800&auto=format&fit=crop", IsMain = true }
                );

                // تطريزات مرتبطة بالمنتج الأول
                context.Embroideries.AddRange(
                    new Embroidery
                    {
                        ProductId = product1.ProductId,
                        ImageUrl = "https://images.unsplash.com/photo-1610030469983-98e550d6193c?w=500&auto=format&fit=crop",
                        Name = "زهرة البرتقال",
                        Description = "إشارة مباشرة إلى بيارات يافا الشهيرة، ترمز هذا النمط إلى الخصوبة والنماء، وتُطرز غالبًا على منطقة الصدر بألوان زاهية."
                    },
                    new Embroidery
                    {
                        ProductId = product1.ProductId,
                        ImageUrl = "https://images.unsplash.com/photo-1610030469520-e6a17a7ee2d3?w=500&auto=format&fit=crop",
                        Name = "شجرة السرو",
                        Description = "رمز للشموخ والبقاء الدائم، يُزين هذا النمط عادة أطراف الثوب وأكمامه، تعبيرًا عن ارتباط الإنسان بأرضه وجذوره العميقة."
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}