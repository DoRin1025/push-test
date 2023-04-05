CREATE TABLE public.apns_registrations
(
    id             bigint                 NOT NULL,
    device_id      character varying(25)  NOT NULL,
    device_token   character varying(300) NOT NULL,
    install_date   TIMESTAMP(3)           NOT NULL,
    last_open_date TIMESTAMP(3)           NOT NULL,
    publisher_id   character varying(50)  NOT NULL,
    username       character varying(50)  NOT NULL,
    app_id         character varying(50)  NOT NULL
);

ALTER TABLE ONLY public.apns_registrations
ADD CONSTRAINT apns_reg_key PRIMARY KEY (id);


CREATE TABLE public.apns_topic_subscriptions
(
    device_id    character varying(50) NOT NULL,
    topic_id     text(50)              NOT NULL,
    topic_type   character varying(50) NOT NULL,
    publisher_id character varying(50) NOT NULL,
    username     character varying(50) NOT NULL,
    app_id       character varying(50) NOT NULL
);

ALTER TABLE ONLY public.apns_registrations
ADD CONSTRAINT apns_reg_key PRIMARY KEY (publisher_id, username, app_id);

--  
-- CREATE TABLE [dbo].[gcm_topic_subscriptions](
--     [publisher_id] [varchar](50) NOT NULL,
--     [username] [varchar](50) NOT NULL,
--     [app_id] [varchar](50) NOT NULL,
--     [device_id] [varchar](50) NOT NULL,
--     [topic_id] [nvarchar](50) NOT NULL,
--     [type] [varchar](50) NOT NULL,
--     CONSTRAINT [PK_gcm_topic_subscriptions] PRIMARY KEY CLUSTERED
-- (
--     [publisher_id] ASC,
--     [username] ASC,
--     [app_id] ASC,
--     [type] ASC,
--     [topic_id] ASC,
-- [device_id] ASC
-- )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
--     ) ON [PRIMARY]
-- 
--     GO
--     
-- /****** Object:  Table [dbo].[pn_topic_subscriptions]    Script Date: 1/19/2021 10:56:32 AM ******/
--     SET ANSI_NULLS ON
--     GO
--     SET QUOTED_IDENTIFIER ON
--     GO
-- CREATE TABLE [dbo].[pn_topic_subscriptions](
--     [username] [varchar](50) NOT NULL,
--     [app_id] [varchar](50) NOT NULL,
--     [device_id] [varchar](50) NOT NULL,
--     [topic_id] [nvarchar](50) NOT NULL,
--     [publisher_id] [varchar](50) NOT NULL,
--     [type] [varchar](50) NOT NULL,
--     CONSTRAINT [PK_pn_topic_subscriptions] PRIMARY KEY CLUSTERED
-- (
--     [publisher_id] ASC,
--     [username] ASC,
--     [app_id] ASC,
--     [type] ASC,
--     [topic_id] ASC,
-- [device_id] ASC
-- )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
--     ) ON [PRIMARY]
-- 
--     GO
-- /****** Object:  Table [dbo].[pwa_app_keys]    Script Date: 1/19/2021 10:56:32 AM ******/
--     SET ANSI_NULLS ON
--     GO
--     SET QUOTED_IDENTIFIER ON
--     GO
-- CREATE TABLE [dbo].[pwa_app_keys](
--     [publisher_id] [varchar](50) NOT NULL,
--     [username] [varchar](50) NOT NULL,
--     [app_id] [varchar](50) NOT NULL,
--     [public_key] [varchar](max) NOT NULL,
--     [private_key] [varchar](max) NOT NULL,
--     CONSTRAINT [PK_pwa_app_keys] PRIMARY KEY CLUSTERED
-- (
--     [publisher_id] ASC,
--     [username] ASC,
-- [app_id] ASC
-- )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
--     ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
-- 
--     GO
-- /****** Object:  Table [dbo].[pwa_registrations]    Script Date: 1/19/2021 10:56:32 AM ******/
--     SET ANSI_NULLS ON
--     GO
--     SET QUOTED_IDENTIFIER ON
--     GO
-- CREATE TABLE [dbo].[pwa_registrations](
--     [publisher_id] [varchar](50) NOT NULL,
--     [username] [varchar](50) NOT NULL,
--     [appid] [varchar](50) NOT NULL,
--     [device_id] [varchar](50) NOT NULL,
--     [endpoint] [varchar](max) NOT NULL,
--     [expirationTime] [varchar](50) NULL,
--     [p256dh] [varchar](max) NOT NULL,
--     [auth] [varchar](max) NOT NULL,
--     [add_date] [datetime] NOT NULL,
--     CONSTRAINT [PK_pwa_registrations] PRIMARY KEY CLUSTERED
-- (
--     [publisher_id] ASC,
--     [username] ASC,
--     [appid] ASC,
-- [device_id] ASC
-- )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
--     ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
-- 
--     GO
-- /****** Object:  Table [dbo].[pwa_topic_subscriptions]    Script Date: 1/19/2021 10:56:32 AM ******/
--     SET ANSI_NULLS ON
--     GO
--     SET QUOTED_IDENTIFIER ON
--     GO
-- CREATE TABLE [dbo].[pwa_topic_subscriptions](
--     [publisher_id] [varchar](50) NOT NULL,
--     [username] [varchar](50) NOT NULL,
--     [app_id] [varchar](50) NOT NULL,
--     [device_id] [varchar](50) NOT NULL,
--     [topic_id] [nvarchar](50) NOT NULL,
--     [type] [varchar](50) NOT NULL,
--     CONSTRAINT [PK_pwa_topic_subscriptions] PRIMARY KEY CLUSTERED
-- (
--     [publisher_id] ASC,
--     [username] ASC,
--     [app_id] ASC,
--     [type] ASC,
--     [topic_id] ASC,
-- [device_id] ASC
-- )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
--     ) ON [PRIMARY]
-- 
--     GO
--     SET ANSI_PADDING ON
-- 
--     GO
-- /****** Object:  Index [PX_D_U_A_P]    Script Date: 1/19/2021 10:56:32 AM ******/
-- CREATE NONCLUSTERED INDEX [PX_D_U_A_P] ON [dbo].[devices]
-- (
-- 	[deviceid] ASC,
-- 	[username] ASC,
-- 	[appid] ASC,
-- 	[publisherid] ASC
-- )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
-- GO
-- SET ANSI_PADDING ON
-- 
-- GO
-- /****** Object:  Index [IX_gcm_registrations_reg_id]    Script Date: 1/19/2021 10:56:32 AM ******/
-- CREATE NONCLUSTERED INDEX [IX_gcm_registrations_reg_id] ON [dbo].[gcm_registrations]
-- (
-- 	[registration_id] ASC
-- )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
-- GO
-- ALTER TABLE [dbo].[devices] ADD  CONSTRAINT [DF_devices_publisherid]  DEFAULT ('') FOR [publisherid]
--     GO
-- ALTER TABLE [dbo].[gcm_registrations] ADD  CONSTRAINT [DF_gcm_registration_publisher_id]  DEFAULT ('') FOR [publisher_id]
--     GO
-- ALTER TABLE [dbo].[gcm_registrations] ADD  CONSTRAINT [DF_gcm_registration_modification_date]  DEFAULT (getdate()) FOR [modification_date]
--     GO
-- ALTER TABLE [dbo].[gcm_registrations] ADD  DEFAULT ((1)) FOR [version]
--     GO
-- ALTER TABLE [dbo].[gcm_topic_subscriptions] ADD  CONSTRAINT [DF_gcm_topic_subscriptions_publisherid]  DEFAULT ('') FOR [publisher_id]
--     GO
-- ALTER TABLE [dbo].[gcm_topic_subscriptions] ADD  DEFAULT ('announcement') FOR [type]
--     GO
-- ALTER TABLE [dbo].[pn_topic_subscriptions] ADD  CONSTRAINT [DF_pn_topic_subscriptions_publisher_id]  DEFAULT ('') FOR [publisher_id]
--     GO
-- ALTER TABLE [dbo].[pn_topic_subscriptions] ADD  DEFAULT ('announcement') FOR [type]
--     GO
-- ALTER TABLE [dbo].[pwa_registrations] ADD  CONSTRAINT [DF_pwa_registrations_add_date]  DEFAULT (getdate()) FOR [add_date]
--     GO
-- ALTER TABLE [dbo].[pwa_topic_subscriptions] ADD  CONSTRAINT [DF_pwa_topic_subscriptions_publisherid]  DEFAULT ('') FOR [publisher_id]
--     GO
-- ALTER TABLE [dbo].[pwa_topic_subscriptions] ADD  DEFAULT ('announcement') FOR [type]
--     GO
-- /****** Object:  StoredProcedure [dbo].[gcm_register]    Script Date: 1/19/2021 10:56:32 AM ******/
--     SET ANSI_NULLS ON
--     GO
--     SET QUOTED_IDENTIFIER ON
--     GO
--     
--     
-- CREATE PROCEDURE [dbo].[gcm_register]
-- @publisher_id varchar(50),
-- @username varchar(50),
-- @app_id varchar(50),
-- @device_id varchar(50),
-- @registration_id varchar(255),
-- @version int
-- 
-- AS
-- 
-- IF EXISTS (SELECT device_id from gcm_registrations
--         WHERE publisher_id = @publisher_id AND username = @username AND app_id = @app_id AND device_id = @device_id)
-- UPDATE gcm_registrations SET registration_id = @registration_id, modification_date = GETDATE(), version = @version
-- WHERE publisher_id = @publisher_id AND username = @username AND app_id = @app_id AND device_id = @device_id
-- 
--     ELSE
-- 
-- INSERT INTO gcm_registrations (publisher_id, username, app_id, device_id, registration_id, modification_date, version)
-- VALUES (@publisher_id, @username, @app_id, @device_id, @registration_id, GETDATE(), @version)
-- 
--     GO
-- /****** Object:  StoredProcedure [dbo].[pwa_register]    Script Date: 1/19/2021 10:56:32 AM ******/
-- SET ANSI_NULLS ON
--     GO
--     SET QUOTED_IDENTIFIER ON
--     GO
-- 
-- CREATE PROCEDURE [dbo].[pwa_register] 
-- @publisherid varchar(50),
-- @username varchar(50),
-- @appid varchar(50),
-- @device_id varchar(MAX),
-- @endpoint varchar(MAX),
-- @p256dh varchar(MAX),
-- @auth varchar(MAX)
-- --,@returnAction int output
-- 
-- AS
-- 
-- IF NOT EXISTS(select endpoint from pwa_registrations where publisher_id=@publisherid and username = @username and appid = @appid and device_id =@device_id)
-- BEGIN
-- 
-- INSERT INTO pwa_registrations
-- (
--     publisher_id,
--     username,
--     appid,
--     device_id,
--     endpoint,
--     p256dh,
--     auth
-- )
-- VALUES (
--            @publisherid,
--            @username,
--            @appid,
--            @device_id,
--            @endpoint,
--            @p256dh,
--            @auth
--        )
-- --set @returnAction = 10
-- END
-- ELSE
-- BEGIN
-- 
-- UPDATE pwa_registrations SET
--                              endpoint = @endpoint,
--                              p256dh = @p256dh,
--                              auth = @auth
-- WHERE
--         publisher_id=@publisherid and username = @username and appid = @appid and device_id =@device_id
-- --set @returnAction = 20
-- END
-- 
-- return
-- 
-- GO
-- /****** Object:  StoredProcedure [dbo].[setdtoken]    Script Date: 1/19/2021 10:56:32 AM ******/
-- SET ANSI_NULLS OFF
-- GO
-- SET QUOTED_IDENTIFIER ON
-- GO
-- CREATE PROCEDURE [dbo].[setdtoken] 
-- @deviceid varchar(50),
-- @username varchar(50),
-- @appid varchar(50),
-- @dtoken image,
-- @publisherid varchar(50)
-- AS
-- SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED
-- IF EXISTS (SELECT deviceid from devices WHERE deviceid = @deviceid AND username=@username AND appid=@appid AND publisherid=@publisherid)
-- UPDATE devices set dtoken = @dtoken, lastopendate = getdate() WHERE deviceid = @deviceid AND username=@username AND appid=@appid AND publisherid=@publisherid
--     ELSE
-- INSERT INTO devices
-- (deviceid, dtoken, installdate, lastopendate, username, appid, publisherid)
-- VALUES
--     (@deviceid, @dtoken, getdate(), getdate(), @username, @appid, @publisherid)
-- 
--     GO
-- /****** Object:  StoredProcedure [dbo].[update_pwa_registrations]    Script Date: 1/19/2021 10:56:32 AM ******/
-- SET ANSI_NULLS ON
--     GO
--     SET QUOTED_IDENTIFIER ON
--     GO
-- 
-- CREATE PROCEDURE [dbo].[update_pwa_registrations] 
-- @publisherid varchar(50),
-- @username varchar(50),
-- @appid varchar(50),
-- @device_id varchar(MAX),
-- @endpoint varchar(MAX),
-- @p256dh varchar(MAX),
-- @auth varchar(MAX)
-- 
-- AS
-- 
-- IF NOT EXISTS(select endpoint from pwa_registrations where publisher_id=@publisherid and username = @username and appid = @appid)
-- BEGIN
-- INSERT INTO pwa_registrations
-- (
--     publisher_id,
--     username,
--     appid,
--     device_id,
--     endpoint,
--     p256dh,
--     auth
-- )
-- VALUES (
--            @publisherid,
--            @username,
--            @appid,
--            @device_id,
--            @endpoint,
--            @p256dh,
--            @auth
--        )
-- END
-- ELSE
-- BEGIN
-- 
-- UPDATE pwa_registrations SET
--                              endpoint = @endpoint,
--                              p256dh = @p256dh,
--                              auth = @auth
-- WHERE
--         publisher_id=@publisherid and username = @username and appid = @appid
-- END
-- 