Name:           Froststrap
Version:        %{?froststrap_version}%{!?froststrap_version:2.0.0}
Release:        1%{?dist}
Summary:        %description

License:        AGPL-3.0
URL:            https://github.com/Froststrap/Froststrap
BuildArch:      x86_64
Requires:       libicu

%description
A fork of Fishstrap, focused on performance and customization

%global __brp_strip /bin/true
%global __brp_strip_comment_note /bin/true

%prep

%build

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}
cp -a %{_froststrap_appdir}/usr %{buildroot}/

%files
/usr/bin/Froststrap
/usr/share/applications/Froststrap.desktop
/usr/share/icons/hicolor/512x512/apps/froststrap.png

%post
if [ -x /usr/bin/update-desktop-database ]; then
    /usr/bin/update-desktop-database -q /usr/share/applications || :
fi
if [ -x /usr/bin/gtk-update-icon-cache ]; then
    /usr/bin/gtk-update-icon-cache -q /usr/share/icons/hicolor || :
fi
/usr/bin/Froststrap --register-mime-types 2>/dev/null || :

%postun
if [ -x /usr/bin/update-desktop-database ]; then
    /usr/bin/update-desktop-database -q /usr/share/applications || :
fi
if [ -x /usr/bin/gtk-update-icon-cache ]; then
    /usr/bin/gtk-update-icon-cache -q /usr/share/icons/hicolor || :
fi
