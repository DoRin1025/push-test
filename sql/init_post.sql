CREATE TABLE public.apns_registrations
(
    id             SERIAL PRIMARY KEY,
    device_id      character varying(50)  NOT NULL,
    device_token   character varying(300) NOT NULL,
    install_date   TIMESTAMP(3)           NOT NULL,
    last_open_date TIMESTAMP(3)           NOT NULL,
    publisher_id   character varying(50)  NOT NULL,
    username       character varying(50)  NOT NULL,
    app_id         character varying(50)  NOT NULL
);

INSERT INTO apns_registrations (device_id, device_token, install_date, last_open_date, publisher_id, username, app_id)
VALUES ('D661347E-9B1D-48EA-AACC-18C0A385A9D3', '19827779EBBE9A57F1D87EEAE8FFE2189E0D2D123A2669641C2A1C3D5DB9B334B',
        CURRENT_TIMESTAMP(3), CURRENT_TIMESTAMP(3), '', 'admin',
        'TestiPhone001');


CREATE TABLE public.apns_topic_subscriptions
(
    id           SERIAL PRIMARY KEY,
    device_id    character varying(50) NOT NULL,
    topic_id     text                  NOT NULL,
    topic_type   character varying(50) NOT NULL,
    publisher_id character varying(50) NOT NULL,
    username     character varying(50) NOT NULL,
    app_id       character varying(50) NOT NULL
);

INSERT INTO apns_topic_subscriptions (device_id, topic_id, topic_type, publisher_id, username, app_id)
VALUES ('D661347E-9B1D-48EA-AACC-18C0A385A9D3', 'test', 'announcement','', 'admin', 'TestiPhone001');
